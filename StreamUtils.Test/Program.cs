using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using System.IO;

namespace StreamUtils.Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
        }
    }

    [TestFixture]
    public class PipeStreamTest {

        public async Task TestSmallerBufferThanData() {
            await AssertPipeWorksWithSizes(10, 11);
            await AssertPipeWorksWithSizes(10, 22);
            await AssertPipeWorksWithSizes(10, 1000);
        }

        public async Task TestBiggerBufferThanData() {
            await AssertPipeWorksWithSizes(11, 10);
            await AssertPipeWorksWithSizes(22, 10);
            await AssertPipeWorksWithSizes(1000, 10);
        }

        public async Task TestSameBufferThanData() {
            await AssertPipeWorksWithSizes(10, 10);
            await AssertPipeWorksWithSizes(1000, 1000);
        }

        public async Task TestManyWritersSingleReader() {

        }

        public async Task ManyReadersSingleWriter() { }

        public async Task ManyReadersManyWriters() { }

        async Task AssertPipeWorksWithSizes(int pipeBufferSize, int dataSize) {
            var rand = new Random();
            var data = new byte[dataSize];
            rand.NextBytes(data);

            using (var subject = new PipeStream(pipeBufferSize))
            using (var bout = new MemoryStream()) {
                var readOperation = subject.CopyToAsync(bout);
                subject.Write(data, 0, data.Length);
                subject.Dispose();
                await readOperation;

                AssertSameArrayContent(data, bout.ToArray(), "data written and read must be the same");
            }
        }

        void AssertSameArrayContent(byte[] a, byte[] b) =>
            Assert.True(a.Length == b.Length && a.Zip(b, (ax, bx) => ax == bx).All(x => x));

        void AssertSameArrayContent(byte[] a, byte[] b, string message) =>
            Assert.True(a.Length == b.Length && a.Zip(b, (ax, bx) => ax == bx).All(x => x), message);

    }
}
