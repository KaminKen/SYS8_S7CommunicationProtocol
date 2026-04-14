using System;
using System.Threading.Tasks;
using SYS8.Core.PubSub;

namespace SYS8.Core.Driver
{
    public class SYS8Monitoring
    {
        private readonly PublishAndSubscribeModel _model;

        public Action<string, object>? OnValueChanged
        {
            get => _model.OnValueChanged;
            set => _model.OnValueChanged = value;
        }

        public bool IsPolling => _model.IsPolling;
        public bool IsEmpty => _model.IsEmpty;

        public SYS8Monitoring(SYS8Driver driver)
        {
            _model = new PublishAndSubscribeModel(driver);
        }

        public Task Subscribe(string topic, string datatype)
        {
            return _model.Subscribe(topic, datatype);
        }

        public void Unsubscribe(string topic)
        {
            _model.Unsubscribe(topic);
        }

        public void StartPolling(int interval = 1000)
        {
            _model.StartPolling(interval);
        }

        public void StopPolling()
        {
            _model.StopPolling();
        }

        public List<string> GetSubscribedTopics()
        {
            return _model.GetSubscribedTopics();
        }
    }
}