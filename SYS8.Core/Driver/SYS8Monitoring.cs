using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SYS8.Core.PubSub;

namespace SYS8.Core.Driver
{
    /// <summary>
    /// Facade around the <see cref="PublishAndSubscribeModel"/> that exposes a simplified
    /// API for subscribing to PLC topics and receiving value updates.
    /// </summary>
    public class SYS8Monitoring
    {
        private readonly PublishAndSubscribeModel _model;

        /// <summary>
        /// Occurs when a subscribed topic value changes. The first parameter is the topic
        /// and the second is the new value (typed as <see cref="object"/>).
        /// </summary>
        public Action<string, object>? OnValueChanged
        {
            get => _model.OnValueChanged;
            set => _model.OnValueChanged = value;
        }

        /// <summary>
        /// Gets a value indicating whether the underlying model is currently polling for updates.
        /// </summary>
        public bool IsPolling => _model.IsPolling;

        /// <summary>
        /// Gets a value indicating whether there are any active subscriptions.
        /// </summary>
        public bool IsEmpty => _model.IsEmpty;

        /// <summary>
        /// Initializes a new instance of the <see cref="SYS8Monitoring"/> class.
        /// </summary>
        /// <param name="driver">The <see cref="SYS8Driver"/> used by the publish/subscribe model.
        /// The driver must be connected before attempting operations that communicate with the PLC.</param>
        public SYS8Monitoring(SYS8Driver driver)
        {
            _model = new PublishAndSubscribeModel(driver);
        }

        /// <summary>
        /// Subscribes to the specified topic with the provided data type.
        /// </summary>
        /// <param name="topic">The topic or address to subscribe to.</param>
        /// <param name="datatype">The data type that should be used when reading the topic.</param>
        /// <returns>A task that completes when the subscription has been registered.</returns>
        public Task Subscribe(string topic, string datatype)
        {
            return _model.Subscribe(topic, datatype);
        }

        /// <summary>
        /// Subscribes to an array topic with the specified length and data type. The topic should
        /// be a valid PLC address, and the length specifies the number of elements in the array.
        /// </summary>
        /// <param name="topic">The topic or address to subscribe to.</param>
        /// <param name="length">The number of elements in the array.</param>
        /// <param name="datatype">The data type that should be used when reading the array elements.</param>
        /// <returns>A task that completes when the subscription has been registered.</returns>
        public Task<string> SubscribeArray(string topic, int length, string datatype)
        {
            return _model.SubscribeArray(topic, length, datatype);
        }


        /// <summary>
        /// Unsubscribes from a previously subscribed topic and also work for array topics. If the topic is an array, it will unsubscribe from all elements of that array.
        /// </summary>
        /// <param name="topic">The topic to unsubscribe.</param>
        public void Unsubscribe(string topic)
        {
            _model.Unsubscribe(topic);
        }


        /// <summary>
        /// Unsubscribes from a range of topics, specified by a starting and optional ending topic.
        /// If the ending topic is not provided, only the starting topic will be unsubscribed.
        /// </summary>
        /// <param name="startingTopic">The starting topic of the range to unsubscribe.</param>
        /// <param name="endingTopic">The optional ending topic of the range to unsubscribe.</param>
        public void UnsubscribeArray(string startingTopic, string? endingTopic = null)
        {
            _model.UnsubscribeArray(startingTopic, endingTopic);
        }

        /// <summary>
        /// Unsubscribe all elements in the subscription list. This will remove all subscriptions and stop receiving updates for any topics.
        /// </summary>
        public void UnsubscribeAll()
        {
            _model.UnsubscribeAll();
        }

        /// <summary>
        /// Starts periodic polling of subscribed topics.
        /// </summary>
        /// <param name="interval">The polling interval in milliseconds. Defaults to 1000 ms.</param>
        public void StartPolling(int interval = 1000)
        {
            _model.StartPolling(interval);
        }

        /// <summary>
        /// Stops periodic polling of subscribed topics.
        /// </summary>
        public void StopPolling()
        {
            _model.StopPolling();
        }

        /// <summary>
        /// Returns a list of currently subscribed topic names.
        /// </summary>
        /// <returns>A <see cref="List{String}"/> containing the subscribed topic identifiers.</returns>
        public List<string> GetSubscribedTopics()
        {
            return _model.GetSubscribedTopics();
        }
    }
}