using System;

namespace Lab4Library.Encryption
{
    public class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> callback;

        public CallbackProgress(Action<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            this.callback = callback;
        }

        public void Report(T value)
        {
            callback(value);
        }
    }
}
