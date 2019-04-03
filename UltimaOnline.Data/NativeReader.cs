using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace UltimaOnline
{
    public static class NativeReader
    {
        static readonly INativeReader _nativeReader;

        static NativeReader()
        {
            _nativeReader = Core.Unix ? new NativeReaderUnix() : (INativeReader)new NativeReaderWin32();
        }

        public static unsafe void Read(IntPtr ptr, void* buffer, int length) => _nativeReader.Read(ptr, buffer, length);
    }

    public interface INativeReader
    {
        unsafe void Read(IntPtr ptr, void* buffer, int length);
    }

    public sealed class NativeReaderWin32 : INativeReader
    {
        internal class UnsafeNativeMethods
        {
            /*[DllImport("kernel32")]
			internal unsafe static extern int _lread(IntPtr hFile, void* lpBuffer, int wBytes);*/

            [DllImport("kernel32")]
            internal unsafe static extern bool ReadFile(IntPtr hFile, void* lpBuffer, uint nNumberOfBytesToRead, ref uint lpNumberOfBytesRead, NativeOverlapped* lpOverlapped);
        }

        public unsafe void Read(IntPtr ptr, void* buffer, int length)
        {
            //UnsafeNativeMethods._lread( ptr, buffer, length );
            var lpNumberOfBytesRead = 0U;
            UnsafeNativeMethods.ReadFile(ptr, buffer, (uint)length, ref lpNumberOfBytesRead, null);
        }
    }

    public sealed class NativeReaderUnix : INativeReader
    {
        internal class UnsafeNativeMethods
        {
            [DllImport("libc")]
            internal unsafe static extern int read(IntPtr ptr, void* buffer, int length);
        }

        public unsafe void Read(IntPtr ptr, void* buffer, int length) => UnsafeNativeMethods.read(ptr, buffer, length);
    }
}