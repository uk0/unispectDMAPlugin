using System;
using System.IO;
using Unispect;
using vmmsharp;

namespace unispectDMAPlugin
{
    [UnispectPlugin]
    public sealed class DMAMemoryPlugin : MemoryProxy
    {
        private readonly Vmm _vmm;
        private uint _pid;

        public DMAMemoryPlugin()
        {
            const string memMapFilename = @"mmap.txt";
            try
            {
                Log.Add("DMA Plugin starting...");
                if (File.Exists(memMapFilename))
                    _vmm = new Vmm("-printf", "-v", "-device", "FPGA", "-memmap", memMapFilename, "-waitinitialize");
                else
                    _vmm = new Vmm("-printf", "-v", "-device", "FPGA", "-waitinitialize");
            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException("ERROR Initializing FPGA", ex);
            }
        }

        public override ModuleProxy GetModule(string moduleName)
        {
            try
            {
                Log.Add("[DMA] Getting module..");
                Vmm.MAP_MODULEENTRY module = _vmm.Map_GetModuleFromName(_pid, moduleName);

                Log.Add($"[DMA] Module Search: {moduleName} | Found: {module.wszText} | BaseAddr: 0x{module.vaBase.ToString("X")} | Size: {module.cbImageSize}");

                return new ModuleProxy(moduleName, module.vaBase, (int)module.cbImageSize);
            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException($"ERROR Getting Module {moduleName}", ex);
            }
        }

        public override bool AttachToProcess(string handle)
        {
            try
            {
                Log.Add("[DMA] Attaching to process");
                // Attach to the process so that the two Read functions are able to interface with the process.
                // The argument: handle (string) will be the text from Unispect's "Process Handle" text box.
                return _vmm.PidGetFromName(handle, out _pid);
            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException($"ERROR attaching to process '{handle}'", ex);
            }
        }

        public override byte[] Read(ulong address, int length)
        {
            try
            {
                var read = _vmm.MemRead(_pid, address, (uint)length);

                // Handle partial read
                if (read.Length != length)
                {
                    byte[] partial = new byte[length];
                    Buffer.BlockCopy(read, 0, partial, 0, read.Length);
                    read = partial;
                }
                return read;
            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException($"ERROR Reading Memory at 0x{address.ToString("X")}", ex);
            }
        }

        public override void Dispose()
        {
            Log.Add("[DMA] Dispose");
            _vmm.Dispose(); // Close FPGA Connection and Release Resources
        }
    }
    public sealed class DMAMemoryPluginException : Exception
    {
        public DMAMemoryPluginException()
        {
        }

        public DMAMemoryPluginException(string message)
            : base(message)
        {
        }

        public DMAMemoryPluginException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}