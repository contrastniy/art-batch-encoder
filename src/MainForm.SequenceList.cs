using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ArtBatchEncoder
{
    internal sealed partial class MainForm
    {
        // Sequence-list actions never modify source files.
        private void SequenceGridCellDoubleClick(object sender, DataGridViewCellEventArgs eventArgs)
        {
            if (eventArgs.RowIndex < 0)
                return;
            OpenSequenceFolder(_sequenceGrid.Rows[eventArgs.RowIndex]);
        }

        private void OpenSelectedFolderClicked(object sender, EventArgs eventArgs)
        {
            if (_sequenceGrid.SelectedRows.Count == 0)
                return;
            OpenSequenceFolder(_sequenceGrid.SelectedRows[0]);
        }

        private void SequenceGridKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode != Keys.Delete || _encoding)
                return;

            RemoveSelectedSequencesClicked(sender, EventArgs.Empty);
            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
        }

        private void RemoveSelectedSequencesClicked(object sender, EventArgs eventArgs)
        {
            if (_encoding || _sequenceGrid == null)
                return;

            var rows = _sequenceGrid.SelectedRows.Cast<DataGridViewRow>()
                .OrderByDescending(row => row.Index)
                .ToList();

            if (rows.Count == 0 && _sequenceGrid.CurrentRow != null)
                rows.Add(_sequenceGrid.CurrentRow);
            if (rows.Count == 0)
                return;

            var removedItems = new HashSet<SequenceItem>();
            foreach (var row in rows)
            {
                var item = row.Tag as SequenceItem;
                if (item != null)
                    removedItems.Add(item);
                _sequenceGrid.Rows.Remove(row);
            }

            if (_loadedBatch != null && removedItems.Count > 0)
            {
                _loadedBatch.Sequences.RemoveAll(item => removedItems.Contains(item));
                foreach (var take in _loadedBatch.Takes)
                    take.Sequences.RemoveAll(item => removedItems.Contains(item));
            }

            var remaining = _sequenceGrid.Rows.Count;
            _progressLabel.Text = remaining == 0
                ? "No sequences in list"
                : remaining.ToString(CultureInfo.InvariantCulture) + " sequence(s) ready";
            _headerStatusLabel.Text = "READY #" + remaining.ToString("0000", CultureInfo.InvariantCulture);

            if (_loadedBatch != null)
            {
                _sourceSummaryLabel.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} take(s) / {1} sequence(s) / {2} warning(s)",
                    _loadedBatch.Takes.Count,
                    remaining,
                    _loadedBatch.Warnings.Count);
            }

            AppendLog(string.Format(
                CultureInfo.InvariantCulture,
                "Removed {0} sequence(s) from the current list. Source files were not changed.",
                rows.Count));
        }

        private static void OpenSequenceFolder(DataGridViewRow row)
        {
            var sequence = row == null ? null : row.Tag as SequenceItem;
            if (sequence == null || !Directory.Exists(sequence.FolderPath))
                return;

            try
            {
                Process.Start("explorer.exe", "\"" + sequence.FolderPath + "\"");
            }
            catch
            {
            }
        }

        private List<DataGridViewRow> GetSelectedRows()
        {
            var rows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in _sequenceGrid.Rows)
            {
                var value = row.Cells[IncludeColumnIndex].Value;
                if (value is bool && (bool)value)
                    rows.Add(row);
            }
            return rows;
        }

        private void SetAllRowsChecked(bool value)
        {
            if (_encoding)
                return;

            foreach (DataGridViewRow row in _sequenceGrid.Rows)
            {
                var item = row.Tag as SequenceItem;
                row.Cells[IncludeColumnIndex].Value = value && item != null && item.IsContiguous;
            }
        }

    }
}
