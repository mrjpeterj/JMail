using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMail.Core
{
    public static class Dependencies
    {
        public static System.Reactive.Concurrency.IScheduler TimeScheduler { get; set; }

        static Dependencies()
        {
            TimeScheduler = System.Reactive.Concurrency.DefaultScheduler.Instance;
        }
    }
}
