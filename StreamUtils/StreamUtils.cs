using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StreamUtils
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var t = Test();
            t.Wait();
            Console.ReadLine();
        }

        private static async Task Test() {
            var testSize = 110;
            var rand = new Random();
            var data = new byte[testSize];
            rand.NextBytes(data);

            var subject = new PipeStream(100);
            var bout = new MemoryStream();
            var tread = subject.CopyToAsync(bout);
            subject.Write(data, 0, data.Length);
            subject.Dispose();
            await tread;

            Console.WriteLine("Equal lengths: {0}", data.Length == bout.Length);
            Console.WriteLine("Equal contents: {0}", bout.ToArray().Zip(data, (a, b) => a == b).All(x => x));

        }
    }

    public class PipeStream : Stream {
        public override bool CanRead {
            get {
                return true;
            }
        }

        public override bool CanSeek {
            get {
                return false;
            }
        }

        public override bool CanWrite {
            get {
                return true;
            }
        }

        public override long Length {
            get {
                throw new NotSupportedException();
            }
        }

        public override long Position {
            get {
                throw new NotSupportedException();
            }

            set {
                throw new NotSupportedException();
            }
        }

        public override void Flush() {
            lock (syncRoot) { }
        }

        protected override void Dispose(bool disposing) {
            lock (syncRoot) {
                finished = true;
                Monitor.Pulse(syncRoot);
            }
            base.Dispose(disposing);
        }

        private readonly int bufferSize;
        private readonly byte[] pipeBuffer;
        private int readp = 0;
        private int writep = 0;
        private int filled = 0;
        private object syncRoot = new object();
        private bool finished = false;

        public PipeStream(): this(512 << 10) { }

        public PipeStream(int bufferSize) {
            this.bufferSize = bufferSize;
            pipeBuffer = new byte[bufferSize];
        }

        public override int Read(byte[] buffer, int offset, int count) {
            if (buffer == null) {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset + count > buffer.Length) {
                throw new ArgumentException("The sum of offset and count is larger than the buffer length");
            }
            if (offset < 0 || count < 0) {
                throw new ArgumentOutOfRangeException("offset or count is negative");
            }

            lock (syncRoot) {
                return InnerRead(buffer, ref offset, ref count);
            }
        }

        private int InnerRead(byte[] buffer, ref int offset, ref int count) {
            while (filled == 0) {
                if (finished == true) {
                    return 0;
                }
                Monitor.Pulse(syncRoot);
                Monitor.Wait(syncRoot);
            }

            if (count > filled) {
                count = filled;
            }

            var read = 0;

            if (readp > writep) {
                if (readp + count > bufferSize) {
                    read = bufferSize - readp;
                    Array.Copy(pipeBuffer, readp, buffer, offset, read);
                    count -= read;
                    offset += read;
                    filled -= read;
                    readp = 0;
                }
            }
            Array.Copy(pipeBuffer, readp, buffer, offset, count);
            readp += count;
            read += count;

            if (readp >= bufferSize) {
                readp -= bufferSize;
            }
            filled -= read;
            Monitor.Pulse(syncRoot);

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            if (buffer == null) {
                throw new ArgumentException(nameof(buffer));
            }
            if (offset + count > buffer.Length) {
                throw new ArgumentException("The sum of offset and count is larger than the buffer length");
            }
            if (offset < 0 || count < 0) {
                throw new ArgumentOutOfRangeException("offset or count is negative");
            }

            lock (syncRoot) {
                InnerWrite(buffer, ref offset, ref count);
                Monitor.Pulse(syncRoot);
            }
        }

        private void InnerWrite(byte[] buffer, ref int offset, ref int count) {
            var available = bufferSize - filled;

            while (count > 0) {
                available = bufferSize - filled;

                while (available == 0) {

                    Monitor.Pulse(syncRoot);
                    Monitor.Wait(syncRoot);
                    
                    available = bufferSize - filled;
                }

                int toWrite;
                if (count > available) {
                    toWrite = available;
                } else {
                    toWrite = count;
                }

                var writepEnd = writep + toWrite;
                if (writepEnd > bufferSize) {
                    var wrote = bufferSize - writep;
                    Array.Copy(buffer, offset, pipeBuffer, writep, wrote);
                    count -= wrote;
                    toWrite -= wrote;
                    offset += wrote;
                    filled += wrote;
                    writep = 0;
                }
                Array.Copy(buffer, offset, pipeBuffer, writep, toWrite);
                count -= toWrite;
                offset += toWrite;
                writep += toWrite;
                filled += toWrite;

                if (writep >= bufferSize) {
                    writep -= bufferSize;
                }
            }
        }
    }
}
