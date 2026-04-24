using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;

namespace SYS8.Core.PubSub
{
    internal class SubscriptionManagementSystem
    {
        private List<SubscriptionItem> GetSubscriptionItems(IEnumerable<SubscriptionItem> subscriptionItems, string datatype)
        {
            // assuming the string value is valid
            var items = subscriptionItems.Where(items => items.DataType == datatype).ToList();
            return items;
        }

        private List<SubscriptionRange> GroupingBooleanSubscription(List<SubscriptionRange> ranges, List<SubscriptionItem> booleanItems, int index, int totalElementCount)
        {
            SubscriptionItem boolElement = booleanItems[index];

            if (ranges.Count == 0)
            {
                // base case no elements
                ranges.Add(new SubscriptionRange
                {
                    DbNumber = boolElement.DbNumber,
                    DataType = boolElement.DataType,
                    StartAbsoluteBitOffset = boolElement.AbsoluteBitOffset,
                    EndAbsoluteBitOffset = boolElement.AbsoluteBitOffset,
                    Items = new List<SubscriptionItem> { boolElement }
                });
            }
            else
            {
                // inductive case, check if the current boolean element can be grouped with the last range
                var lastRange = ranges.Last();
                if (lastRange.DbNumber == boolElement.DbNumber && lastRange.EndAbsoluteBitOffset + 1 == boolElement.AbsoluteBitOffset)
                {
                    lastRange.EndAbsoluteBitOffset = boolElement.AbsoluteBitOffset;
                    lastRange.Items.Add(boolElement);
                }
                else
                {
                    ranges.Add(new SubscriptionRange
                    {
                        DbNumber = boolElement.DbNumber,
                        DataType = boolElement.DataType,
                        StartAbsoluteBitOffset = boolElement.AbsoluteBitOffset,
                        EndAbsoluteBitOffset = boolElement.AbsoluteBitOffset,
                        Items = new List<SubscriptionItem> { boolElement }
                    });
                }
            }
            if (index != totalElementCount - 1)
            {
                return GroupingBooleanSubscription(ranges, booleanItems, index + 1, totalElementCount);
            }
            else
            {
                return ranges;
            }
        }

        private List<SubscriptionRange> Grouping16BitsElementsSubscription(List<SubscriptionRange> ranges, List<SubscriptionItem> ElementsWith16BitsItems, int index, int totalElementCount)
        {
            SubscriptionItem ElementsWith16Bits = ElementsWith16BitsItems[index];

            if (ranges.Count == 0)
            {
                // base case no elements
                ranges.Add(new SubscriptionRange
                {
                    DbNumber = ElementsWith16Bits.DbNumber,
                    DataType = ElementsWith16Bits.DataType,
                    StartAbsoluteBitOffset = ElementsWith16Bits.AbsoluteBitOffset, //byte * 8
                    EndAbsoluteBitOffset = ElementsWith16Bits.AbsoluteBitOffset + 15, // setting to the last bits of the second data bytes
                    Items = new List<SubscriptionItem> { ElementsWith16Bits }
                });
            }
            else
            {
                // inductive case, check if the current int16 element can be grouped with the last range
                var lastRange = ranges.Last();
                if (lastRange.DbNumber == ElementsWith16Bits.DbNumber && (lastRange.EndingByteOffset) + 1 == ElementsWith16Bits.ByteOffset) //we compare bytes
                {
                    lastRange.EndAbsoluteBitOffset = ElementsWith16Bits.AbsoluteBitOffset + 15; //2 bytes
                    lastRange.Items.Add(ElementsWith16Bits);
                }
                else
                {
                    ranges.Add(new SubscriptionRange
                    {
                        DbNumber = ElementsWith16Bits.DbNumber,
                        DataType = ElementsWith16Bits.DataType,
                        StartAbsoluteBitOffset = ElementsWith16Bits.AbsoluteBitOffset,
                        EndAbsoluteBitOffset = ElementsWith16Bits.AbsoluteBitOffset + 15, //2 bytes
                        Items = new List<SubscriptionItem> { ElementsWith16Bits }
                    });
                }
            }
            if (index != totalElementCount - 1)
            {
                return Grouping16BitsElementsSubscription(ranges, ElementsWith16BitsItems, index + 1, totalElementCount);
            }
            else
            {
                return ranges;
            }
        }

        private List<SubscriptionRange> Grouping32BitsElementsSubscription(List<SubscriptionRange> ranges, List<SubscriptionItem> ElementsWith32BitsItems, int index, int totalElementCount)
        {
            SubscriptionItem ElementsWith32Bits = ElementsWith32BitsItems[index];

            if (ranges.Count == 0)
            {
                // base case no elements
                ranges.Add(new SubscriptionRange
                {
                    DbNumber = ElementsWith32Bits.DbNumber,
                    DataType = ElementsWith32Bits.DataType,
                    StartAbsoluteBitOffset = ElementsWith32Bits.AbsoluteBitOffset, //byte * 8
                    EndAbsoluteBitOffset = ElementsWith32Bits.AbsoluteBitOffset + 31, // setting to the last bits of the second data bytes
                    Items = new List<SubscriptionItem> { ElementsWith32Bits }
                });
            }
            else
            {
                // inductive case, check if the current int16 element can be grouped with the last range
                var lastRange = ranges.Last();
                if (lastRange.DbNumber == ElementsWith32Bits.DbNumber && (lastRange.EndingByteOffset) + 1 == ElementsWith32Bits.ByteOffset) //we compare bytes
                {
                    lastRange.EndAbsoluteBitOffset = ElementsWith32Bits.AbsoluteBitOffset + 31; //4 bytes
                    lastRange.Items.Add(ElementsWith32Bits);
                }
                else
                {
                    ranges.Add(new SubscriptionRange
                    {
                        DbNumber = ElementsWith32Bits.DbNumber,
                        DataType = ElementsWith32Bits.DataType,
                        StartAbsoluteBitOffset = ElementsWith32Bits.AbsoluteBitOffset,
                        EndAbsoluteBitOffset = ElementsWith32Bits.AbsoluteBitOffset + 31, //4 bytes
                        Items = new List<SubscriptionItem> { ElementsWith32Bits }
                    });
                }
            }
            if (index != totalElementCount - 1)
            {
                return Grouping32BitsElementsSubscription(ranges, ElementsWith32BitsItems, index + 1, totalElementCount);
            }
            else
            {
                return ranges;
            }
        }

        private List<SubscriptionRange> Grouping64BitsElementsSubscription(List<SubscriptionRange> ranges, List<SubscriptionItem> ElementsWith64BitsItems, int index, int totalElementCount)
        {
            SubscriptionItem ElementsWith64Bits = ElementsWith64BitsItems[index];

            if (ranges.Count == 0)
            {
                // base case no elements
                ranges.Add(new SubscriptionRange
                {
                    DbNumber = ElementsWith64Bits.DbNumber,
                    DataType = ElementsWith64Bits.DataType,
                    StartAbsoluteBitOffset = ElementsWith64Bits.AbsoluteBitOffset, //byte * 8
                    EndAbsoluteBitOffset = ElementsWith64Bits.AbsoluteBitOffset + 63, // setting to the last bits of the second data bytes
                    Items = new List<SubscriptionItem> { ElementsWith64Bits }
                });
            }
            else
            {
                // inductive case, check if the current int16 element can be grouped with the last range
                var lastRange = ranges.Last();
                if (lastRange.DbNumber == ElementsWith64Bits.DbNumber && (lastRange.EndingByteOffset) + 1 == ElementsWith64Bits.ByteOffset) //we compare bytes
                {
                    lastRange.EndAbsoluteBitOffset = ElementsWith64Bits.AbsoluteBitOffset + 63; //8 bytes
                    lastRange.Items.Add(ElementsWith64Bits);
                }
                else
                {
                    ranges.Add(new SubscriptionRange
                    {
                        DbNumber = ElementsWith64Bits.DbNumber,
                        DataType = ElementsWith64Bits.DataType,
                        StartAbsoluteBitOffset = ElementsWith64Bits.AbsoluteBitOffset,
                        EndAbsoluteBitOffset = ElementsWith64Bits.AbsoluteBitOffset + 63, //8 bytes
                        Items = new List<SubscriptionItem> { ElementsWith64Bits }
                    });
                }
            }
            if (index != totalElementCount - 1)
            {
                return Grouping64BitsElementsSubscription(ranges, ElementsWith64BitsItems, index + 1, totalElementCount);
            }
            else
            {
                return ranges;
            }
        }



        public List<SubscriptionRange> GetBooleanSubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var booleanItems = GetSubscriptionItems(subscriptionItems, "bool");
            List<SubscriptionRange> ranges = new List<SubscriptionRange>();

            if (booleanItems.Count == 0)
            {
                return ranges; //empty list if no boolean items found
            }
            
            booleanItems = booleanItems.OrderBy(item => item.DbNumber)
                                       .ThenBy(item => item.AbsoluteBitOffset)
                                       .ToList();
            

            return GroupingBooleanSubscription(ranges, booleanItems, 0, booleanItems.Count);
        }

        public List<SubscriptionRange> GetInt16SubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var int16Items = GetSubscriptionItems(subscriptionItems, "int16");
            List<SubscriptionRange> ranges = new List<SubscriptionRange>();

            if (int16Items.Count == 0)
            {
                return ranges; //empty list if no int16 items found
            }

            int16Items = int16Items.OrderBy(item => item.DbNumber)
                                       .ThenBy(item => item.AbsoluteBitOffset)
                                       .ToList();


            return Grouping16BitsElementsSubscription(ranges, int16Items, 0, int16Items.Count);
        }

        // For Int32

        public List<SubscriptionRange> GetUInt16SubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var uint16Items = GetSubscriptionItems(subscriptionItems, "uint16");
            List<SubscriptionRange> ranges = new List<SubscriptionRange>();

            if (uint16Items.Count == 0)
            {
                return ranges; //empty list if no int16 items found
            }

            uint16Items = uint16Items.OrderBy(item => item.DbNumber)
                                       .ThenBy(item => item.AbsoluteBitOffset)
                                       .ToList();


            return Grouping16BitsElementsSubscription(ranges, uint16Items, 0, uint16Items.Count);
        }

        public List<SubscriptionRange> GetInt32SubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var int32Items = GetSubscriptionItems(subscriptionItems, "int32");
            List<SubscriptionRange> ranges = new List<SubscriptionRange>();

            if (int32Items.Count == 0)
            {
                return ranges; //empty list if no int32 items found
            }

            int32Items = int32Items.OrderBy(item => item.DbNumber)
                                       .ThenBy(item => item.AbsoluteBitOffset)
                                       .ToList();


            return Grouping32BitsElementsSubscription(ranges, int32Items, 0, int32Items.Count);
        }

        public List<SubscriptionRange> GetUInt32SubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var uint32Items = GetSubscriptionItems(subscriptionItems, "uint32");
            List<SubscriptionRange> ranges = new List<SubscriptionRange>();

            if (uint32Items.Count == 0)
            {
                return ranges; //empty list if no int32 items found
            }

            uint32Items = uint32Items.OrderBy(item => item.DbNumber)
                                       .ThenBy(item => item.AbsoluteBitOffset)
                                       .ToList();


            return Grouping32BitsElementsSubscription(ranges, uint32Items, 0, uint32Items.Count);
        }

        public List<SubscriptionRange> GetFloat32SubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var float32Items = GetSubscriptionItems(subscriptionItems, "float32");
            List<SubscriptionRange> ranges = new List<SubscriptionRange>();

            if (float32Items.Count == 0)
            {
                return ranges; //empty list if no float32 items found
            }

            float32Items = float32Items.OrderBy(item => item.DbNumber)
                                       .ThenBy(item => item.AbsoluteBitOffset)
                                       .ToList();


            return Grouping32BitsElementsSubscription(ranges, float32Items, 0, float32Items.Count);
        }

        public List<SubscriptionRange> GetFloat64SubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var float64Items = GetSubscriptionItems(subscriptionItems, "float64");
            List<SubscriptionRange> ranges = new List<SubscriptionRange>();

            if (float64Items.Count == 0)
            {
                return ranges; //empty list if no float64 items found
            }

            float64Items = float64Items.OrderBy(item => item.DbNumber)
                                       .ThenBy(item => item.AbsoluteBitOffset)
                                       .ToList();


            return Grouping64BitsElementsSubscription(ranges, float64Items, 0, float64Items.Count);
        }

    }
}
