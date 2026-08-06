using System;

namespace ArtBatchEncoder
{
    internal sealed class CodecProfile
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Extension { get; private set; }
        public string CpuEncoder { get; private set; }
        public string CpuArguments { get; private set; }
        public string NvidiaEncoder { get; private set; }
        public string NvidiaArguments { get; private set; }
        public string AmdEncoder { get; private set; }
        public string AmdArguments { get; private set; }
        public string IntelEncoder { get; private set; }
        public string IntelArguments { get; private set; }
        public string Description { get; private set; }
        public bool PreservesAlpha { get; private set; }

        public CodecProfile(
            string id,
            string name,
            string extension,
            string cpuEncoder,
            string cpuArguments,
            string nvidiaEncoder,
            string nvidiaArguments,
            string amdEncoder,
            string amdArguments,
            string intelEncoder,
            string intelArguments,
            string description,
            bool preservesAlpha)
        {
            Id = id;
            Name = name;
            Extension = extension;
            CpuEncoder = cpuEncoder;
            CpuArguments = cpuArguments;
            NvidiaEncoder = nvidiaEncoder;
            NvidiaArguments = nvidiaArguments;
            AmdEncoder = amdEncoder;
            AmdArguments = amdArguments;
            IntelEncoder = intelEncoder;
            IntelArguments = intelArguments;
            Description = description;
            PreservesAlpha = preservesAlpha;
        }

        public bool SupportsGpu
        {
            get
            {
                return !string.IsNullOrWhiteSpace(NvidiaEncoder) ||
                       !string.IsNullOrWhiteSpace(AmdEncoder) ||
                       !string.IsNullOrWhiteSpace(IntelEncoder);
            }
        }

        public string GetEncoderName(bool useGpu, string backend)
        {
            if (!useGpu)
                return CpuEncoder;

            if (string.Equals(backend, GpuBackends.Nvidia, StringComparison.OrdinalIgnoreCase))
                return NvidiaEncoder;
            if (string.Equals(backend, GpuBackends.Amd, StringComparison.OrdinalIgnoreCase))
                return AmdEncoder;
            if (string.Equals(backend, GpuBackends.Intel, StringComparison.OrdinalIgnoreCase))
                return IntelEncoder;

            return null;
        }

        public string GetVideoArguments(bool useGpu, string backend)
        {
            if (!useGpu)
                return CpuArguments;

            if (string.Equals(backend, GpuBackends.Nvidia, StringComparison.OrdinalIgnoreCase))
                return NvidiaArguments;
            if (string.Equals(backend, GpuBackends.Amd, StringComparison.OrdinalIgnoreCase))
                return AmdArguments;
            if (string.Equals(backend, GpuBackends.Intel, StringComparison.OrdinalIgnoreCase))
                return IntelArguments;

            return null;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
