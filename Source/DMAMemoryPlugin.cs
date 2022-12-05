using System;
using System.IO;
using Unispect;
using vmmsharp;

namespace unispectDMAPlugin
{
    [UnispectPlugin]
    public sealed class DMAMemoryPlugin : MemoryProxy
    {
        // Using a static field since Unispect does not Dispose the plugin after each operation.
        // However it will attempt to re-run the constructor each time (leading to an FPGA Init error).
        // Using a static will save the plugin state between operations.
        private static Vmm _vmm;
        private static bool _loaded = false;
        private uint _pid;

        public DMAMemoryPlugin()
        {
            const string memMapFilename = @"mmap.txt";
            try
            {
                if (_loaded) return;
                Log.Add("[DMA] Plugin Starting...");
                if (File.Exists(memMapFilename))
                {
                    Log.Add("[DMA] Memory Map Found!");
                    _vmm = new Vmm("-printf", "-v", "-device", "FPGA", "-memmap", memMapFilename, "-waitinitialize");
                }
                else
                    _vmm = new Vmm("-printf", "-v", "-device", "FPGA", "-waitinitialize");
                _loaded = true;
                Log.Add("[DMA] Plugin Loaded!");
            }
            catch (Exception ex)
            {
                Log.Add($"[DMA] ERROR Initializing FPGA: {ex}");
                throw new DMAMemoryPluginException("ERROR Initializing FPGA", ex);
            }
        }

        public override ModuleProxy GetModule(string moduleName)
        {
            try
            {
                Log.Add($"[DMA] Getting module '{moduleName}'");
                Vmm.MAP_MODULEENTRY module = _vmm.Map_GetModuleFromName(_pid, moduleName);

                Log.Add($"[DMA] Module Search: {moduleName} | Found: {module.wszText} | BaseAddr: 0x{module.vaBase.ToString("X")} | Size: {module.cbImageSize}");

                return new ModuleProxy(moduleName, module.vaBase, (int)module.cbImageSize);
            }
            catch (Exception ex)
            {
                Log.Add($"[DMA] ERROR Getting module '{moduleName}': {ex}");
                throw new DMAMemoryPluginException($"ERROR Getting module '{moduleName}'", ex);
            }
        }

        public override bool AttachToProcess(string handle)
        {
            try
            {
                Log.Add($"[DMA] Attaching to process '{handle}'");
                // Slightly differs from Unispect's default Memory Plugin
                // Use 'ProcessName.exe' instead of 'ProcessName'.
                return _vmm.PidGetFromName(handle, out _pid);
            }
            catch (Exception ex)
            {
                Log.Add($"[DMA] ERROR attaching to process '{handle}': {ex}");
                throw new DMAMemoryPluginException($"ERROR attaching to process '{handle}'", ex);
            }
        }

        public override byte[] Read(ulong address, int length)
        {
            try
            {
                // Fixed partial reads bug from original
                return _vmm.MemRead(_pid, address, (uint)length);
            }
            catch (Exception ex)
            {
                Log.Add($"[DMA] ERROR Reading Memory at 0x{address.ToString("X")}: {ex}");
                throw new DMAMemoryPluginException($"ERROR Reading Memory at 0x{address.ToString("X")}", ex);
            }
        }

        public override void Dispose()
        {
            Log.Add("[DMA] Dispose");
            _vmm.Dispose(); // Close FPGA Connection and Release Resources
            _vmm = null;
            _loaded = false;
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