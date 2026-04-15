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
        private Dictionary<string, string> _datatype = new();
        private Dictionary<string, object> _previousValues = new();
        private CancellationTokenSource? _pollingCts;

        public Action<string, object>? OnValueChanged { get; set; }

        public bool IsEmpty => _previousValues.Count == 0;
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
            return _previousValues.Keys.ToList();
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

            if(_previousValues.ContainsKey(topic)) 
            {
                throw new InvalidOperationException($"Topic '{topic}' is already subscribed.");
            }

            _datatype[topic] = datatype.ToLower(); 
            object initialValue =  datatype.ToLower() switch {
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
            _previousValues[topic] = initialValue;
        }

        public async Task SubscribeArray(string? topic, uint length, string? datatype) // , Action<string, object> callback
        {
            if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(datatype))
            {
                throw new ArgumentException("Topic and datatype cannot be null or empty.");
            }


            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }

            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(topic);

            int initialOffset = byteOffset * 8 + bitIndex;
            for (int i = initialOffset; i < initialOffset + length; i++)
            { 
                int localByteOffset = i / 8;
                int localBitIndex = i % 8;
                string tempTopic = "DB" + dbNumber + ".DBX" + localByteOffset + "." + localBitIndex;

                if (_previousValues.ContainsKey(tempTopic))
                {
                    throw new InvalidOperationException($"Topic '{tempTopic}' is already subscribed.");
                }

                _datatype[tempTopic] = datatype.ToLower();
                object initialValue = datatype.ToLower() switch
                {
                    "bool" => await _driver.ReadBoolAsync(dbNumber, localByteOffset, localBitIndex),
                    "int16" => await _driver.ReadInt16Async(dbNumber, localByteOffset, localBitIndex),
                    "uint16" => await _driver.ReadUInt16Async(dbNumber, localByteOffset, localBitIndex),
                    "int32" => await _driver.ReadInt32Async(dbNumber, localByteOffset, localBitIndex),
                    "uint32" => await _driver.ReadUInt32Async(dbNumber, localByteOffset, localBitIndex),
                    "float32" => await _driver.ReadFloat32Async(dbNumber, localByteOffset, localBitIndex),
                    "float64" => await _driver.ReadFloat64Async(dbNumber, localByteOffset, localBitIndex),
                    "string" => string.Empty,
                    _ => throw new ArgumentException($"Unsupported data type: {datatype}")
                };
                _previousValues[tempTopic] = initialValue;
            }
        }


        public void Unsubscribe(string topic)
        {
            // Implementation for unsubscribing from a topic
            // This would involve removing the callback and topic from the data structure
            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }
            if (!_previousValues.ContainsKey(topic))
            {
                throw new InvalidOperationException($"Topic '{topic}' is not subscribed.");
            }
            _datatype.Remove(topic);
            _previousValues.Remove(topic);
        }

        public async Task CheckForUpdates()
        {
            // Implementation for checking for updates on subscribed topics
            // This would involve comparing the current value with the previous value and invoking callbacks if they differ
            if (!_driver.IsConnected)
            {
                throw new InvalidOperationException("Driver is not connected.");
            }
            foreach (var topic in _previousValues.Keys.ToList()) //make a copy of keys to avoid modification during iteration
            {
                object newValue = _datatype[topic] switch
                {
                    "bool" => await _driver.ReadBoolAsync(topic),
                    "int16" => await _driver.ReadInt16Async(topic),
                    "uint16" => await _driver.ReadUInt16Async(topic),
                    "int32" => await _driver.ReadInt32Async(topic),
                    "uint32" => await _driver.ReadUInt32Async(topic),
                    "float32" => await _driver.ReadFloat32Async(topic),
                    "float64" => await _driver.ReadFloat64Async(topic),
                    "string" => string.Empty,
                    _ => throw new ArgumentException($"Unsupported data type: {_datatype[topic]}")
                };
                if (!Equals(newValue, _previousValues[topic]))
                {
                    // Value has changed, invoke callbacks here (not implemented in this snippet)
                    // For example: InvokeCallbacks(topic, newValue);
                    // Update previous value
                    _previousValues[topic] = newValue;
                    OnValueChanged?.Invoke(topic, newValue);
                   
                }
            }
        }

    }
}
