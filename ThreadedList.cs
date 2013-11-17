using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Threading;
using System.Collections.Specialized;

namespace JMail
{
    public class ThreadedList<T>: IList<T>, INotifyCollectionChanged
    {
        delegate void VoidCall();

        Dispatcher dispatcher_;
        DispatcherTimer updateTimer_;

        List<NotifyCollectionChangedEventArgs> pending_;

        List<T> items_;

        public ThreadedList()
        {
            dispatcher_ = Dispatcher.FromThread(System.Threading.Thread.CurrentThread);
            updateTimer_ = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Background, FlushPending, dispatcher_);
            updateTimer_.IsEnabled = false;

            items_ = new List<T>();

            pending_ = new List<NotifyCollectionChangedEventArgs>();
        }

        void PostCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (dispatcher_ != null)
            {
                lock (this)
                {
                    pending_.Add(e);

                    // Turn on the flusher
                    updateTimer_.IsEnabled = true;
                }
            }
            else
            {
                CollectionChanged(this, e);
            }
        }

        void FlushPending(object sender, EventArgs args)
        {
            lock (this)
            {
                foreach (var e in pending_)
                {
                    CollectionChanged(this, e);

                    if (e.Action == NotifyCollectionChangedAction.Reset)
                    {
                        break;
                    }
                }

                pending_.Clear();

                updateTimer_.IsEnabled = false;
            }
        }

        #region IList<T> Members

        public int IndexOf(T item)
        {
            lock (this)
            {
                return items_.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (this)
            {
                items_.Insert(index, item);

                if (CollectionChanged != null)
                {
                    PostCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }

        public void RemoveAt(int index)
        {
            lock (this)
            {
                items_.RemoveAt(index);

                if (CollectionChanged != null)
                {
                    PostCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }

        public T this[int index]
        {
            get
            {
                lock (this)
                {
                    return items_[index];
                }
            }
            set
            {
                lock (this)
                {
                    var oldItem = items_[index];
                    items_[index] = value;

                    if (CollectionChanged != null)
                    {
                        PostCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, oldItem, value));
                    }
                }
            }
        }

        #endregion

        #region ICollection<T> Members

        public void Add(T item)
        {
            lock (this)
            {
                items_.Add(item);

                if (CollectionChanged != null)
                {
                    PostCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
                }
            }
        }

        public void Clear()
        {
            lock (this)
            {
                items_.Clear();

                if (CollectionChanged != null)
                {
                    PostCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }

        public bool Contains(T item)
        {
            lock (this)
            {
                return items_.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (this)
            {
                items_.CopyTo(array, arrayIndex);
            }
        }

        public int Count
        {
            get
            {
                lock (this)
                {
                    return items_.Count;
                }
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            lock (this)
            {
                bool res = items_.Remove(item);
                if (res && CollectionChanged != null)
                {
                    PostCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }

                return res;
            }
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            return items_.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return items_.GetEnumerator();
        }

        #endregion

        #region INotifyCollectionChanged Members

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        #endregion
    }
}
