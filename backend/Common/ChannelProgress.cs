using CdArchiveBackend.Data;
using System;
using System.Net.ServerSentEvents;
using System.Threading.Channels;

namespace CdArchiveBackend.Common
{
    // Wraps IProgress.Report in a Channel so that we can guarantee that they are delivered in order
    internal sealed class ChannelProgress(ChannelWriter<SseItem<object>> writer) : IProgress<StreamingProgress>
    {
        public void Report(StreamingProgress value) => writer.TryWrite(new SseItem<object>(value, "progress"));
    }
}
