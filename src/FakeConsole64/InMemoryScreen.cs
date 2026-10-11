using Fake64;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FakeConsole64;

public class InMemoryScreen: IScreen
{
    byte[] buffer = new byte[403 * 284 * 4]; // 403x284 pixels, 4 bytes per pixel (ARGB)
    private GCHandle handle;

    unsafe public byte* BeginRender()
    {
        this.handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        return (byte*)handle.AddrOfPinnedObject();
    }

    unsafe public void EndRender()
    {
        handle.Free();
    }
}
