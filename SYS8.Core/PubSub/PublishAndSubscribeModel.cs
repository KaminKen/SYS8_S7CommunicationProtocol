using SYS8.Core.Driver;
using SYS8.Core.StringManipulation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace SYS8.Core.PubSub
{
    internal class PublishAndSubscribeModel
    {
        private readonly SYS8Driver _driver;
        //private Dictionary<string, string> _datatype = new();
        //private Dictionary<string, object> _previousValues = new();
        private readonly Dictionary<string, SubscriptionItem> _subscriptions = new();
        private readonly object _lock = new object();
        private CancellationTokenSource? _pollingCts;
        private readonly SynchronizationContext? _syncContext;

        private bool _boolRangesDirty = true;
        private List<SubscriptionRange> _cachedBoolRanges = new();

        private void UpdateAllRangeDirtiness(bool isDirty)
        {
            _boolRangesDirty = isDirty;
        }

        private void UpdateRangeDirtinessByDataType(string datatype, bool isDirty)
        {
            switch (datatype.ToLowerInvariant())
            {
                // Implement other datatype later
                case "bool":
                    _boolRangesDirty = isDirty;
                    break;
                default:
                    // To be sure, all are now dirty
                    UpdateAllRangeDirtiness(isDirty);
                    break;
            }
        }

        public Action<string, object>? OnValueChanged { get; set; }

        public bool IsEmpty
        {
            get
            {
                lock (_lock) 
                {
                    return _subscriptions.Count == 0;
                }
            }
        }
        public bool IsPolling
        {
            get 
            {
                lock (_lock) 
                {
                    return _pollingCts != null;
                }
            }
        }

        public PublishAndSubscribeModel(SYS8Driver driver)
        {
            _driver = driver;
            _syncContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Parse a Siemens DB address string (for example "DB1.DBX0.1") into numeric components.
        /// </summary>
        /// <param name="address">Address string to parse.</param>
        /// <returns>Tuple of (dbNumber, byteOffset, bitIndex).</returns>
        private (ushort dbNumber, int byteOffset, int bitIndex) ParseStringAddress(string address)
        {
            var parser = new StringAddressToAbsoluteAddress();
            return parser.ParseStringAddress(address);
        }

        private string ConvertToAbsoluteAddress(ushort dbNumber, int byteOffset, int bitIndex)
        {
            var parser = new StringAddressToAbsoluteAddress();
            return parser.ConvertToAbsoluteAddress(dbNumber, byteOffset, bitIndex);
        }

        public void StartPolling(int interval = 1000)
        {
            lock (_lock)
            {
                if (_pollingCts != null)
                {
                    throw new InvalidOperationException("Polling is already running.");
                }

                if (IsEmpty)
                {
                    throw new InvalidOperationException("No topics subscribed.");
                }
            }

            _pollingCts = new CancellationTokenSource();  // create fresh token
            var token = _pollingCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await CheckForUpdates();
                        await Task.Delay(interval, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // normal shutdown
                }
            }, token);
        }

        public void StopPolling()
        {
            lock (_lock)
            {
                _pollingCts?.Cancel();
                _pollingCts = null;
            }
        }

        public List<string> GetSubscribedTopics()
        {
            lock (_lock)
            {
                return _subscriptions.Keys.ToList();
            }
        }



        public async Task Subscribe(string? topic, string? datatype) // , Action<string, object> callback
        {
            // Implementation for subscribing to a topic
            // This would involve storing the callback and topic in a data structure
           
            if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(datatype))
                throw new ArgumentException("Topic and datatype cannot be null or empty.");

            if (!_driver.IsConnected)
                throw new InvalidOperationException("Driver is not connected.");

            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(topic);
            string normalizedType = datatype.ToLowerInvariant();

            // perform initial read (may be slow); do not hold lock while awaiting
            object initialValue = normalizedType switch
            {
                "bool" => await _driver.ReadBoolAsync(topic),
                "int16" => await _driver.ReadInt16Async(topic),
                "uint16" => await _driver.ReadUInt16Async(topic),
                "int32" => await _driver.ReadInt32Async(topic),
                "uint32" => await _driver.ReadUInt32Async(topic),
                "float32" => await _driver.ReadFloat32Async(topic),
                "float64" => await _driver.ReadFloat64Async(topic),
                "string" => string.Empty,
                _ => throw new ArgumentException($"Unsupported data type: {datatype}")
            };

            lock (_lock)
            {
                if (_subscriptions.ContainsKey(topic))
                {
                    throw new InvalidOperationException($"Topic '{topic}' is already subscribed.");
                }

                _subscriptions[topic] = new SubscriptionItem
                {
                    Topic = topic,
                    DataType = normalizedType,
                    PreviousValue = initialValue,
                    DbNumber = dbNumber,
                    ByteOffset = byteOffset,
                    BitIndex = bitIndex
                };
                UpdateRangeDirtinessByDataType(normalizedType, true);
            }
        }


        public async Task<string> SubscribeArray(string startingTopic, string endingTopic, string datatype)
        {
            if (string.IsNullOrEmpty(startingTopic) || string.IsNullOrEmpty(endingTopic) || string.IsNullOrEmpty(datatype))
            {
                throw new ArgumentException("Starting topic, ending topic, and datatype cannot be null or empty.");
            }
            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }
            var normalizedType = datatype.ToLowerInvariant();
            var (startDbNumber, startByteOffset, startBitIndex) = ParseStringAddress(startingTopic);
            var (endDbNumber, endByteOffset, endBitIndex) = ParseStringAddress(endingTopic);
            if (startDbNumber != endDbNumber)
            {
                throw new ArgumentException("Starting and ending topics must be in the same DB.");
            }
            int length = (endByteOffset * 8 + endBitIndex) - (startByteOffset * 8 + startBitIndex) + 1;
            return await SubscribeArray(startingTopic, length, datatype);
        }


        public async Task<string> SubscribeArray(string startingTopic, int length, string datatype) // , Action<string, object> callback
        {
            if (string.IsNullOrEmpty(startingTopic) || string.IsNullOrEmpty(datatype))
                throw new ArgumentException("Topic and datatype cannot be null or empty.");

            //there is checking if length is > 0 inside Read Method


            if (!_driver.IsConnected)
                throw new InvalidOperationException("Driver is not connected.");

            var normalizedType = datatype.ToLowerInvariant();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(startingTopic);

            object[] returnValueArray;
            switch (normalizedType)
            {
                //only bool array is supported for now, other types can be added later if needed
                case "bool":
                    bool[] boolArray = await _driver.ReadBoolArrayAsync(startingTopic, length);
                    returnValueArray = boolArray.Cast<object>().ToArray();
                    break;
                default:
                    throw new ArgumentException($"Unsupported data type: {datatype}");
            }

            string lastTopic = startingTopic;

            for (int i = 0; i < length; i++)
            {
                int absoluteBitOffset = byteOffset * 8 + bitIndex + i;
                int localByteOffset = absoluteBitOffset / 8;
                int localBitIndex = absoluteBitOffset % 8;

                string tempTopic = ConvertToAbsoluteAddress(dbNumber, localByteOffset, localBitIndex);

                lock (_lock)
                {
                    if (_subscriptions.ContainsKey(tempTopic))
                    {
                        throw new InvalidOperationException($"Topic '{tempTopic}' is already subscribed.");
                    }

                    _subscriptions[tempTopic] = new SubscriptionItem
                    {
                        Topic = tempTopic,
                        DataType = normalizedType,
                        PreviousValue = returnValueArray[i],
                        DbNumber = dbNumber,
                        ByteOffset = localByteOffset,
                        BitIndex = localBitIndex
                    };
                    UpdateRangeDirtinessByDataType(normalizedType, true);
                }
                lastTopic = tempTopic;
            }
            return lastTopic; //last topic in the array, can be used for unsubscribing the whole array later 
        }


        public void Unsubscribe(string topic)
        {
            // Implementation for unsubscribing from a topic
            // This would involve removing the callback and topic from the data structure
            if (!_driver.IsConnected)
                throw new InvalidOperationException("Driver is not connected.");

            lock (_lock)
            {
                if (!_subscriptions.ContainsKey(topic))
                    throw new InvalidOperationException($"Topic '{topic}' is not subscribed.");

                UpdateRangeDirtinessByDataType(_subscriptions[topic].DataType, true);

                _subscriptions.Remove(topic);
            }
        }

        public void UnsubscribeArray(string startingTopic, string? endingTopic = null)
        {
            //if endingTopic is null then it will unsubscribe all array elements starting from startingTopic
            if (!_driver.IsConnected)
                throw new InvalidOperationException("Driver is not connected.");

            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(startingTopic);
            int startAbsoluteBitOffset = byteOffset * 8 + bitIndex;
            int endAbsoluteBitOffset = startAbsoluteBitOffset; //place holder

            if (!string.IsNullOrEmpty(endingTopic))
            {
                var (endDbNumber, endByteOffset, endBitIndex) = ParseStringAddress(endingTopic);
                if (dbNumber != endDbNumber)
                {
                    throw new ArgumentException("Starting and ending topics must be in the same DB.");
                }
                endAbsoluteBitOffset = endByteOffset * 8 + endBitIndex;
                if (endAbsoluteBitOffset < startAbsoluteBitOffset)
                {
                    throw new ArgumentException("Ending topic must not be before starting topic.");
                }
            }

            List<string> topicsToRemove;
            lock (_lock)
            {
                topicsToRemove = _subscriptions.Values.Where(s => s.DbNumber == dbNumber)
                    .Where(s => (s.AbsoluteBitOffset) >= startAbsoluteBitOffset && (s.AbsoluteBitOffset) <= endAbsoluteBitOffset)
                    .Select(s => s.Topic)
                    .ToList();

                foreach (var topic in topicsToRemove)
                {
                    _subscriptions.Remove(topic);
                }
                UpdateAllRangeDirtiness(true);
            }
        }

        public void UnsubscribeAll()
        {
            if (!_driver.IsConnected)
                throw new InvalidOperationException("Driver is not connected.");

            lock (_lock)
            {
                _subscriptions.Clear();
                UpdateAllRangeDirtiness(true);
            }
        }   


        public async Task CheckForUpdates()
        {
            // Implementation for checking for updates on subscribed topics
            // This would involve comparing the current value with the previous value and invoking callbacks if they differ
            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }

            List<SubscriptionItem> subsSnapShot;
            lock (_lock)
            {
                subsSnapShot = _subscriptions.Values.ToList(); // snapshot under lock
            }

            var boolSubs = subsSnapShot.Where(s => s.DataType == "bool").ToList();
            var nonBoolSubs = subsSnapShot.Where(s => s.DataType != "bool").ToList();

            if (boolSubs.Count > 0)
            {
                List<SubscriptionRange> boolRanges;

                lock (_lock)
                {
                    if (_boolRangesDirty)
                    {
                        var sms = new SubscriptionManagementSystem();
                        _cachedBoolRanges = sms.GetBooleanSubscriptionSortedRange(_subscriptions.Values);
                        _boolRangesDirty = false; //cleaned
                    }

                    boolRanges = _cachedBoolRanges.ToList();
                }

                Debug.WriteLine($"Found {boolRanges.Count} boolean ranges to read.");
                foreach (var range in boolRanges)
                {
                    Debug.WriteLine($"Range: {range.StartAbsoluteBitOffset} to {range.EndAbsoluteBitOffset}, Items: {range.Items.Count}");
                }

                foreach (var range in boolRanges)
                {
                    int length = range.EndAbsoluteBitOffset - range.StartAbsoluteBitOffset + 1;
                    bool[] newEachRangeValues = await _driver.ReadBoolArrayAsync(range.Items.First().Topic, length); // Read the entire range at once
                    for (int i = 0; i < range.Items.Count; i++)
                    {
                        var sub = range.Items[i];
                        bool newValue = newEachRangeValues[i];
                        if (!Equals(newValue, sub.PreviousValue))
                        {
                            sub.PreviousValue = newValue;
                            OnValueChanged?.Invoke(sub.Topic, newValue);
                        }
                    }
                }
            }

            foreach (var items in nonBoolSubs)
            {
                //if (processedTopics.Contains(topic))
                //    continue;


                // use local copy of subscription item (from snapshot)
                var sub = items;

                object newValue = sub.DataType switch
                {
                    "int16" => await _driver.ReadInt16Async(sub.Topic),
                    "uint16" => await _driver.ReadUInt16Async(sub.Topic),
                    "int32" => await _driver.ReadInt32Async(sub.Topic),
                    "uint32" => await _driver.ReadUInt32Async(sub.Topic),
                    "float32" => await _driver.ReadFloat32Async(sub.Topic),
                    "float64" => await _driver.ReadFloat64Async(sub.Topic),
                    "string" => string.Empty,
                    _ => throw new ArgumentException($"Unsupported data type: {sub.DataType}")
                };

                if (!Equals(newValue, sub.PreviousValue))
                {
                    // update the live subscription item if it still exists
                    lock (_lock)
                    {
                        if (_subscriptions.TryGetValue(sub.Topic, out var live))
                        {
                            live.PreviousValue = newValue;
                        }
                    }

                    // invoke callback on captured synchronization context if present
                    if (_syncContext != null)
                    {
                        _syncContext.Post(_ => OnValueChanged?.Invoke(sub.Topic, newValue), null);
                    }
                    else
                    {
                        OnValueChanged?.Invoke(sub.Topic, newValue);
                    }
                }
            }
        }

    }
}
