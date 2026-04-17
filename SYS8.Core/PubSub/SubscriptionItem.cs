using System;
using System.Collections.Generic;
using System.Text;

namespace SYS8.Core.PubSub
{
    internal class SubscriptionItem
    {
        public string Topic { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public object? PreviousValue { get; set; }

        public ushort DbNumber { get; set; }
        public int ByteOffset { get; set; }
        public int BitIndex { get; set; }

        public bool IsArrayElement { get; set; }
        public string? ArrayRootTopic { get; set; }
        public string? ArrayEndingTopic { get; set; }
        public int ArrayIndex { get; set; }

        public int? ArrayLength { get; set; }
    }
}
