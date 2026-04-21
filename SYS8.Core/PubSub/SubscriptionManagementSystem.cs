using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;

namespace SYS8.Core.PubSub
{
    internal class SubscriptionManagementSystem
    {
        private List<SubscriptionItem> GetBooleanSubscriptions(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var booleanItems = subscriptionItems.Where(items => items.DataType == "bool").ToList();
            return booleanItems;
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

        public List<SubscriptionRange> GetBooleanSubscriptionSortedRange(IEnumerable<SubscriptionItem> subscriptionItems)
        {
            var booleanItems = GetBooleanSubscriptions(subscriptionItems);
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
    }
}
