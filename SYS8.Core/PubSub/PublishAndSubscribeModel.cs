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
using System.Runtime.InteropServices;

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
        private bool _int16RangesDirty = true;
        private bool _int32RangesDirty = true;
        private bool _uint16RangesDirty = true;
        private bool _uint32RangesDirty = true;
        private bool _floatRangesDirty = true;
        private bool _doubleRangesDirty = true;     
        private List<SubscriptionRange> _cachedBoolRanges = new();
        private List<SubscriptionRange> _cachedInt16Ranges = new();
        private List<SubscriptionRange> _cachedInt32Ranges = new();
        private List<SubscriptionRange> _cachedUInt16Ranges = new();
        private List<SubscriptionRange> _cachedUInt32Ranges = new();
        private List<SubscriptionRange> _cachedFloatRanges = new();
        private List<SubscriptionRange> _cachedDoubleRanges = new();

        private void UpdateAllRangeDirtiness(bool isDirty)
        {
            _boolRangesDirty = isDirty;
            _int16RangesDirty = isDirty;
            _int32RangesDirty = isDirty;
            _uint16RangesDirty = isDirty;
            _uint32RangesDirty = isDirty;
            _floatRangesDirty = isDirty;
            _doubleRangesDirty = isDirty;
        }

        private void UpdateRangeDirtinessByDataType(string datatype, bool isDirty)
        {
            switch (datatype.ToLowerInvariant())
            {
                // Implement other datatype later
                case "bool":
                    _boolRangesDirty = isDirty;
                    break;
                case "int16":
                    _int16RangesDirty = isDirty;
                    break;
                case "int32":
                    _int32RangesDirty = isDirty;
                    break;
                case "uint16":
                    _uint16RangesDirty = isDirty;
                    break;
                case "uint32":
                    _uint32RangesDirty = isDirty;
                    break;
                case "float32":
                    _floatRangesDirty = isDirty;
                    break;
                case "float64":
                    _doubleRangesDirty = isDirty;
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



        public async Task Subscribe(string? topic, string? datatype, int? maxStringLength = null) // , Action<string, object> callback
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
                // read Siemens STRING when max length provided; otherwise leave empty
                "string" => maxStringLength.HasValue ? await _driver.ReadStringAsync(topic, maxStringLength.Value) : string.Empty,
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

            // deliver initial value to caller(s) so UI can display it immediately
            if (initialValue != null)
            {
                if (_syncContext != null)
                {
                    _syncContext.Post(_ => OnValueChanged?.Invoke(topic, initialValue), null);
                }
                else
                {
                    OnValueChanged?.Invoke(topic, initialValue);
                }
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

            int length = 0;

            // compute inclusive length based on data type size
            switch (normalizedType)
            {
                case "bool":
                    length = (endByteOffset * 8 + endBitIndex) - (startByteOffset * 8 + startBitIndex) + 1;
                    break;
                case "int16":
                case "uint16":
                    length = ((endByteOffset - startByteOffset) / 2) + 1;
                    break;
                case "int32":
                case "uint32":
                case "float32":
                    length = ((endByteOffset - startByteOffset) / 4) + 1;
                    break;
                case "float64":
                    length = ((endByteOffset - startByteOffset) / 8) + 1;
                    break;
                default:
                    throw new ArgumentException($"Unsupported data type: {datatype}");
            }

            return await SubscribeArray(startingTopic, length, datatype);
        }

        private string BooleanBitsUpdate(string startingTopic, int absoluteBitLength, string normalizedType, object[] returnValueArray)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(startingTopic);
            string lastTopic = startingTopic;
            for (int i = 0; i < absoluteBitLength; i++)
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
            return lastTopic;
        }


        public async Task<string> SubscribeArray(string startingTopic, int numberOfElements, string datatype) // , Action<string, object> callback
        {

            if (string.IsNullOrEmpty(startingTopic) || string.IsNullOrEmpty(datatype))
                throw new ArgumentException("Topic and datatype cannot be null or empty.");

            //there is checking if length is > 0 inside Read Method


            if (!_driver.IsConnected)
                throw new InvalidOperationException("Driver is not connected.");

            var normalizedType = datatype.ToLowerInvariant();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(startingTopic);

            object[] returnValueArray;
            // Read arrays using numeric overloads to avoid re-parsing addresses repeatedly
            switch (normalizedType)
            {
                case "bool":
                    {
                        bool[] boolArray = await _driver.ReadBoolArrayAsync(dbNumber, byteOffset, bitIndex, numberOfElements);
                        Debug.WriteLine($"SubscribeArray: read bool array start={byteOffset}.{bitIndex} db={dbNumber} length={numberOfElements} returned={boolArray.Length}");
                        returnValueArray = boolArray.Cast<object>().ToArray();
                        break;
                    }
                case "int16":
                    {
                        short[] int16Array = await _driver.ReadInt16ArrayAsync(dbNumber, byteOffset, bitIndex, numberOfElements);
                        returnValueArray = int16Array.Cast<object>().ToArray();
                        break;
                    }
                case "int32":
                    {
                        int[] int32Array = await _driver.ReadInt32ArrayAsync(dbNumber, byteOffset, bitIndex, numberOfElements);
                        returnValueArray = int32Array.Cast<object>().ToArray();
                        break;
                    }
                case "uint16":
                    {
                        ushort[] uint16Array = await _driver.ReadUInt16ArrayAsync(dbNumber, byteOffset, bitIndex, numberOfElements);
                        returnValueArray = uint16Array.Cast<object>().ToArray();
                        break;
                    }
                case "uint32":
                    {
                        uint[] uint32Array = await _driver.ReadUInt32ArrayAsync(dbNumber, byteOffset, bitIndex, numberOfElements);
                        returnValueArray = uint32Array.Cast<object>().ToArray();
                        break;
                    }
                case "float32":
                    {
                        float[] float32Array = await _driver.ReadFloat32ArrayAsync(dbNumber, byteOffset, bitIndex, numberOfElements);
                        returnValueArray = float32Array.Cast<object>().ToArray();
                        break;
                    }
                case "float64":
                    {
                        double[] float64Array = await _driver.ReadFloat64ArrayAsync(dbNumber, byteOffset, bitIndex, numberOfElements);
                        returnValueArray = float64Array.Cast<object>().ToArray();
                        break;
                    }
                default:
                    throw new ArgumentException($"Unsupported data type: {datatype}");
            }

            string lastTopic = startingTopic;

            if (normalizedType == "bool")
            {
                lastTopic = BooleanBitsUpdate(startingTopic, numberOfElements, normalizedType, returnValueArray);
            }
            else if (normalizedType == "int16" || normalizedType == "int32" || normalizedType == "uint16" || normalizedType == "uint32" || normalizedType == "float32" || normalizedType == "float64")
            {
                for (int i = 0; i < numberOfElements; i++)
                {
                    int byteOffsetIncrement = i * (normalizedType switch
                    {
                        "int16" => 2,
                        "uint16" => 2,
                        "int32" => 4,
                        "uint32" => 4,
                        "float32" => 4,
                        "float64" => 8,
                        _ => throw new ArgumentException($"Unsupported data type: {datatype}")
                    });
                    int localByteOffset = byteOffset + byteOffsetIncrement; //byteOffset of startingTopic + increment based on data type size
                    string tempTopic = ConvertToAbsoluteAddress(dbNumber, localByteOffset, 0);
                    lock (_lock)
                    {
                        if (_subscriptions.ContainsKey(tempTopic))
                        {
                            throw new InvalidOperationException($"Topic '{tempTopic}' is already subscribed.");
                            // may change to continue and append this log to users
                        }
                        _subscriptions[tempTopic] = new SubscriptionItem
                        {
                            Topic = tempTopic,
                            DataType = normalizedType,
                            PreviousValue = returnValueArray.Length > i ? returnValueArray[i] : null,
                            DbNumber = dbNumber,
                            ByteOffset = localByteOffset,
                            BitIndex = 0 // as all the datatype except bool take the whole bytes to store value so it must start at 0
                        };
                        // PreviousValue already set from the typed array above.
                        UpdateRangeDirtinessByDataType(normalizedType, true);
                    }
                    lastTopic = tempTopic;
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported data type: {datatype}");
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
            var int16Subs = subsSnapShot.Where(s => s.DataType == "int16").ToList();
            var int32Subs = subsSnapShot.Where(s => s.DataType == "int32").ToList();
            var uint16Subs = subsSnapShot.Where(s => s.DataType == "uint16").ToList();
            var uint32Subs = subsSnapShot.Where(s => s.DataType == "uint32").ToList();
            var float32Subs = subsSnapShot.Where(s => s.DataType == "float32").ToList();
            var float64Subs = subsSnapShot.Where(s => s.DataType == "float64").ToList();

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
                    bool[] newEachRangeValues = await _driver.ReadBoolArrayAsync(range.Items.First().DbNumber, range.Items.First().ByteOffset, range.Items.First().BitIndex, length); // Read the entire range at once
                    for (int i = 0; i < range.Items.Count; i++)
                    {
                        var subTemplate = range.Items[i];
                        bool newValue = newEachRangeValues[i];
                        // update live subscription under lock
                        bool changed = false;
                        lock (_lock)
                        {
                            if (_subscriptions.TryGetValue(subTemplate.Topic, out var live))
                            {
                                if (!Equals(live.PreviousValue, newValue))
                                {
                                    live.PreviousValue = newValue;
                                    changed = true;
                                }
                            }
                        }

                        if (changed)
                        {
                            if (_syncContext != null)
                            {
                                _syncContext.Post(_ => OnValueChanged?.Invoke(subTemplate.Topic, newValue), null);
                            }
                            else
                            {
                                OnValueChanged?.Invoke(subTemplate.Topic, newValue);
                            }
                        }
                    }
                }
            }

            // handle int16 ranges (coalesced reads)
            if (int16Subs.Count > 0)
            {
                List<SubscriptionRange> int16Ranges;

                lock (_lock)
                {
                    if (_int16RangesDirty)
                    {
                        var sms = new SubscriptionManagementSystem();
                        _cachedInt16Ranges = sms.GetInt16SubscriptionSortedRange(_subscriptions.Values);
                        _int16RangesDirty = false; //cleaned
                    }

                    int16Ranges = _cachedInt16Ranges.ToList();
                }


                Debug.WriteLine($"Found {int16Ranges.Count} int16 ranges to read.");

                foreach (var range in int16Ranges)
                {
                    Debug.WriteLine($"Range: {range.StartingByteOffset} to {range.EndingByteOffset}, Items: {range.Items.Count}");
                }

                foreach (var range in int16Ranges)
                {
                    //int bits = range.EndAbsoluteBitOffset - range.StartAbsoluteBitOffset + 1;
                    int elementCount = (range.EndingByteOffset - range.StartingByteOffset)/2 + 1; // number of int16 elements in the range

                    if (elementCount <= 0) continue;

                    var first = range.Items.First();
                    short[] values = await _driver.ReadInt16ArrayAsync(first.DbNumber, first.ByteOffset, first.BitIndex, elementCount);

                    for (int i = 0; i < range.Items.Count && i < values.Length; i++)
                    {
                        var subTemplate = range.Items[i];
                        var newValue = values[i];
                        bool changed = false;
                        lock (_lock)
                        {
                            if (_subscriptions.TryGetValue(subTemplate.Topic, out var live))
                            {
                                if (!Equals(live.PreviousValue, newValue))
                                {
                                    live.PreviousValue = newValue;
                                    changed = true;
                                }
                            }
                        }

                        if (changed)
                        {
                            if (_syncContext != null)
                                _syncContext.Post(_ => OnValueChanged?.Invoke(subTemplate.Topic, newValue), null);
                            else
                                OnValueChanged?.Invoke(subTemplate.Topic, newValue);
                        }
                    }
                }
            }

            // handle uint16 ranges (coalesced reads)
            if (uint16Subs.Count > 0)
            {
                List<SubscriptionRange> uint16Ranges;

                lock (_lock)
                {
                    if (_uint16RangesDirty)
                    {
                        var sms = new SubscriptionManagementSystem();
                        _cachedUInt16Ranges = sms.GetUInt16SubscriptionSortedRange(_subscriptions.Values);
                        _uint16RangesDirty = false; //cleaned
                    }

                    uint16Ranges = _cachedUInt16Ranges.ToList();
                }
                Debug.WriteLine($"Found {uint16Ranges.Count} uint16 ranges to read.");

                foreach (var range in uint16Ranges)
                {
                    Debug.WriteLine($"Range: {range.StartingByteOffset} to {range.EndingByteOffset}, Items: {range.Items.Count}");
                }

                foreach (var range in uint16Ranges)
                {
                    //int bits = range.EndAbsoluteBitOffset - range.StartAbsoluteBitOffset + 1;
                    int elementCount = (range.EndingByteOffset - range.StartingByteOffset)/2 + 1; // number of uint16 elements in the range

                    var first = range.Items.First();
                    ushort[] values = await _driver.ReadUInt16ArrayAsync(first.DbNumber, first.ByteOffset, first.BitIndex, elementCount);

                    for (int i = 0; i < range.Items.Count && i < values.Length; i++)
                    {
                        var subTemplate = range.Items[i];
                        var newValue = values[i];
                        bool changed = false;
                        lock (_lock)
                        {
                            if (_subscriptions.TryGetValue(subTemplate.Topic, out var live))
                            {
                                if (!Equals(live.PreviousValue, newValue))
                                {
                                    live.PreviousValue = newValue;
                                    changed = true;
                                }
                            }
                        }

                        if (changed)
                        {
                            if (_syncContext != null)
                                _syncContext.Post(_ => OnValueChanged?.Invoke(subTemplate.Topic, newValue), null);
                            else
                                OnValueChanged?.Invoke(subTemplate.Topic, newValue);
                        }
                    }
                }
            }

            if (int32Subs.Count > 0)
            {
                List<SubscriptionRange> int32Ranges;

                lock (_lock)
                {
                    if (_int32RangesDirty)
                    {
                        var sms = new SubscriptionManagementSystem();
                        _cachedInt32Ranges = sms.GetInt32SubscriptionSortedRange(_subscriptions.Values);
                        _int32RangesDirty = false; //cleaned
                    }

                    int32Ranges = _cachedInt32Ranges.ToList();
                }
                Debug.WriteLine($"Found {int32Ranges.Count} int32 ranges to read.");

                foreach (var range in int32Ranges)
                {
                    Debug.WriteLine($"Range: {range.StartingByteOffset} to {range.EndingByteOffset}, Items: {range.Items.Count}");
                }

                foreach (var range in int32Ranges)
                {
                    //int bits = range.EndAbsoluteBitOffset - range.StartAbsoluteBitOffset + 1;
                    int elementCount = (range.EndingByteOffset - range.StartingByteOffset) / 4 + 1; // number of int32 elements in the range

                    var first = range.Items.First();
                    int[] values = await _driver.ReadInt32ArrayAsync(first.DbNumber, first.ByteOffset, first.BitIndex, elementCount);

                    for (int i = 0; i < range.Items.Count && i < values.Length; i++)
                    {
                        var subTemplate = range.Items[i];
                        var newValue = values[i];
                        bool changed = false;
                        lock (_lock)
                        {
                            if (_subscriptions.TryGetValue(subTemplate.Topic, out var live))
                            {
                                if (!Equals(live.PreviousValue, newValue))
                                {
                                    live.PreviousValue = newValue;
                                    changed = true;
                                }
                            }
                        }

                        if (changed)
                        {
                            if (_syncContext != null)
                                _syncContext.Post(_ => OnValueChanged?.Invoke(subTemplate.Topic, newValue), null);
                            else
                                OnValueChanged?.Invoke(subTemplate.Topic, newValue);
                        }
                    }
                }
            }

            if (uint32Subs.Count > 0)
            {
                List<SubscriptionRange> uint32Ranges;

                lock (_lock)
                {
                    if (_uint32RangesDirty)
                    {
                        var sms = new SubscriptionManagementSystem();
                        _cachedUInt32Ranges = sms.GetUInt32SubscriptionSortedRange(_subscriptions.Values);
                        _uint32RangesDirty = false; //cleaned
                    }

                    uint32Ranges = _cachedUInt32Ranges.ToList();
                }
                Debug.WriteLine($"Found {uint32Ranges.Count} uint32 ranges to read.");

                foreach (var range in uint32Ranges)
                {
                    Debug.WriteLine($"Range: {range.StartingByteOffset} to {range.EndingByteOffset}, Items: {range.Items.Count}");
                }

                foreach (var range in uint32Ranges)
                {
                    //int bits = range.EndAbsoluteBitOffset - range.StartAbsoluteBitOffset + 1;
                    int elementCount = (range.EndingByteOffset - range.StartingByteOffset) / 4 + 1; // number of uint32 elements in the range

                    var first = range.Items.First();
                    uint[] values = await _driver.ReadUInt32ArrayAsync(first.DbNumber, first.ByteOffset, first.BitIndex, elementCount);

                    for (int i = 0; i < range.Items.Count && i < values.Length; i++)
                    {
                        var subTemplate = range.Items[i];
                        var newValue = values[i];
                        bool changed = false;
                        lock (_lock)
                        {
                            if (_subscriptions.TryGetValue(subTemplate.Topic, out var live))
                            {
                                if (!Equals(live.PreviousValue, newValue))
                                {
                                    live.PreviousValue = newValue;
                                    changed = true;
                                }
                            }
                        }

                        if (changed)    
                        {
                            if (_syncContext != null)
                                _syncContext.Post(_ => OnValueChanged?.Invoke(subTemplate.Topic, newValue), null);
                            else
                                OnValueChanged?.Invoke(subTemplate.Topic, newValue);
                        }
                    }
                }
            }

            // handle remaining non-boolean single-element subscriptions (int32/uint32/float/string)
            var nonBool = subsSnapShot.Where(s => s.DataType != "bool" && s.DataType != "int16" && s.DataType != "uint16").ToList();
            foreach (var subTemplate in nonBool)
            {
                object newValue = subTemplate.DataType switch
                {
                    //"int32" => await _driver.ReadInt32Async(subTemplate.DbNumber, subTemplate.ByteOffset, subTemplate.BitIndex),
                    //"uint32" => await _driver.ReadUInt32Async(subTemplate.DbNumber, subTemplate.ByteOffset, subTemplate.BitIndex),
                    "float32" => await _driver.ReadFloat32Async(subTemplate.DbNumber, subTemplate.ByteOffset, subTemplate.BitIndex),
                    "float64" => await _driver.ReadFloat64Async(subTemplate.DbNumber, subTemplate.ByteOffset, subTemplate.BitIndex),
                    "string" => string.Empty,
                    _ => throw new ArgumentException($"Unsupported data type: {subTemplate.DataType}")
                };

                if (!Equals(newValue, subTemplate.PreviousValue))
                {
                    lock (_lock)
                    {
                        if (_subscriptions.TryGetValue(subTemplate.Topic, out var live))
                        {
                            live.PreviousValue = newValue;
                        }
                    }

                    if (_syncContext != null)
                    {
                        _syncContext.Post(_ => OnValueChanged?.Invoke(subTemplate.Topic, newValue), null);
                    }
                    else
                    {
                        OnValueChanged?.Invoke(subTemplate.Topic, newValue);
                    }
                }
            }
        }

    }
}
