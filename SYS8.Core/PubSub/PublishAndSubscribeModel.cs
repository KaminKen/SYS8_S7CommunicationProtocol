using SYS8.Core.Driver;
using SYS8.Core.StringManipulation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;

namespace SYS8.Core.PubSub
{
    internal class PublishAndSubscribeModel
    {
        private readonly SYS8Driver _driver;
        //private Dictionary<string, string> _datatype = new();
        //private Dictionary<string, object> _previousValues = new();
        private readonly Dictionary<string, SubscriptionItem> _subscriptions = new();
        private CancellationTokenSource? _pollingCts;

        public Action<string, object>? OnValueChanged { get; set; }

        public bool IsEmpty => _subscriptions.Count == 0;
        public bool IsPolling => _pollingCts != null;

        public PublishAndSubscribeModel(SYS8Driver driver)
        {
            _driver = driver;
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
            if (_pollingCts != null)
            {
                throw new InvalidOperationException("Polling is already running.");
            }

            if (IsEmpty)
            {
                throw new InvalidOperationException("No topics subscribed.");
            }

            _pollingCts = new CancellationTokenSource();  // create fresh token
            var token = _pollingCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await CheckForUpdates();
                    await Task.Delay(interval, token);
                }
            }, token);
        }

        public void StopPolling()
        {
            _pollingCts?.Cancel();
            _pollingCts = null;
        }

        public List<string> GetSubscribedTopics()
        {
            return _subscriptions.Keys.ToList();
        }



        public async Task Subscribe(string? topic, string? datatype) // , Action<string, object> callback
        {
            // Implementation for subscribing to a topic
            // This would involve storing the callback and topic in a data structure
           
            if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(datatype))
            {
                throw new ArgumentException("Topic and datatype cannot be null or empty.");
            }


            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }

            if(_subscriptions.ContainsKey(topic)) 
            {
                throw new InvalidOperationException($"Topic '{topic}' is already subscribed.");
            }

            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(topic);
            string normalizedType = datatype.ToLower();

            object initialValue =  normalizedType switch {
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
            _subscriptions[topic] = new SubscriptionItem
            {
                Topic = topic,
                DataType = normalizedType,
                PreviousValue = initialValue,
                DbNumber = dbNumber,
                ByteOffset = byteOffset,
                BitIndex = bitIndex,
                IsArrayElement = false,
                ArrayRootTopic = null,
                ArrayIndex = -1,
                ArrayLength = 0
            };
        }


        public async Task<string> SubscribeArray(string? topic, int length, string? datatype) // , Action<string, object> callback
        {
            if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(datatype))
            {
                throw new ArgumentException("Topic and datatype cannot be null or empty.");
            }

            //there is checking if length is > 0 inside Read Method


            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }

            var normalizedType = datatype.ToLower();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(topic);

            object[] returnValueArray;
            switch (datatype.ToLower())
            {
                case "bool":
                    bool[] boolArray = await _driver.ReadBoolArrayAsync(topic, length);
                    returnValueArray = boolArray.Cast<object>().ToArray();
                    break;
                default:
                    throw new ArgumentException($"Unsupported data type: {datatype}");
            }

            string lastTopic = topic;

            for (int i = 0; i < length; i++)
            {
                int absoluteBitOffset = byteOffset * 8 + bitIndex + i;
                int localByteOffset = absoluteBitOffset / 8;
                int localBitIndex = absoluteBitOffset % 8;

                string tempTopic = ConvertToAbsoluteAddress(dbNumber, localByteOffset, localBitIndex);

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
                    BitIndex = localBitIndex,
                    IsArrayElement = true,
                    ArrayRootTopic = topic,
                    ArrayIndex = i,
                    ArrayLength = length
                };
                lastTopic = tempTopic;

                //object initialValue = datatype.ToLower() switch
                //{
                //    "bool" => await _driver.ReadBoolAsync(dbNumber, localByteOffset, localBitIndex),
                //    "int16" => await _driver.ReadInt16Async(dbNumber, localByteOffset, localBitIndex),
                //    "uint16" => await _driver.ReadUInt16Async(dbNumber, localByteOffset, localBitIndex),
                //    "int32" => await _driver.ReadInt32Async(dbNumber, localByteOffset, localBitIndex),
                //    "uint32" => await _driver.ReadUInt32Async(dbNumber, localByteOffset, localBitIndex),
                //    "float32" => await _driver.ReadFloat32Async(dbNumber, localByteOffset, localBitIndex),
                //    "float64" => await _driver.ReadFloat64Async(dbNumber, localByteOffset, localBitIndex),
                //    "string" => string.Empty,
                //    _ => throw new ArgumentException($"Unsupported data type: {datatype}")
                //};
                //_previousValues[tempTopic] = initialValue;
            }
            return lastTopic; //last topic in the array, can be used for unsubscribing the whole array later 
        }


        public void Unsubscribe(string topic)
        {
            // Implementation for unsubscribing from a topic
            // This would involve removing the callback and topic from the data structure
            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }
            if (!_subscriptions.ContainsKey(topic))
            {
                throw new InvalidOperationException($"Topic '{topic}' is not subscribed.");
            }
            _subscriptions.Remove(topic);
        }

        public async Task CheckForUpdates()
        {
            // Implementation for checking for updates on subscribed topics
            // This would involve comparing the current value with the previous value and invoking callbacks if they differ
            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }

            var processedTopics = new HashSet<string>();

            var boolArrayGroups = _subscriptions.Values
                .Where(s => s.DataType == "bool" && s.IsArrayElement && !string.IsNullOrEmpty(s.ArrayRootTopic))
                .GroupBy(s => s.ArrayRootTopic);

            foreach (var group in boolArrayGroups)
            {
                string rootTopic = group.Key!;
                int length = group.Max(x => x.ArrayLength ?? 0);

                bool[] newValues = await _driver.ReadBoolArrayAsync(rootTopic, length);

                foreach (var sub in group)
                {
                    bool newValue = newValues[sub.ArrayIndex];
                    if (!Equals(newValue, sub.PreviousValue))
                    {
                        sub.PreviousValue = newValue;
                        OnValueChanged?.Invoke(sub.Topic, newValue);
                    }

                    processedTopics.Add(sub.Topic);
                }
            }

            foreach (var topic in _subscriptions.Keys.ToList())
            {
                if (processedTopics.Contains(topic))
                    continue;

                var sub = _subscriptions[topic];

                object newValue = sub.DataType switch
                {
                    "bool" => await _driver.ReadBoolAsync(topic),
                    "int16" => await _driver.ReadInt16Async(topic),
                    "uint16" => await _driver.ReadUInt16Async(topic),
                    "int32" => await _driver.ReadInt32Async(topic),
                    "uint32" => await _driver.ReadUInt32Async(topic),
                    "float32" => await _driver.ReadFloat32Async(topic),
                    "float64" => await _driver.ReadFloat64Async(topic),
                    "string" => string.Empty,
                    _ => throw new ArgumentException($"Unsupported data type: {sub.DataType}")
                };

                if (!Equals(newValue, sub.PreviousValue))
                {
                    sub.PreviousValue = newValue;
                    OnValueChanged?.Invoke(topic, newValue);
                }
            }
        }

    }
}
