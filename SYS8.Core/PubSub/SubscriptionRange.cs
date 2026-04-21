using System;
using System.Collections.Generic;
using System.Text;

namespace SYS8.Core.PubSub
{
    internal class SubscriptionRange
    {
        public ushort DbNumber { get; set; }
        public string DataType { get; set; } = string.Empty;

        public int StartAbsoluteBitOffset { get; set; }
        public int EndAbsoluteBitOffset { get; set; }

        public List<SubscriptionItem> Items { get; set; } = new();
    }
}
