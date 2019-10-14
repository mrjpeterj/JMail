using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMail
{
    interface IChangingProperty : INotifyPropertyChanged
    {
        void OnPropertyChanged(string propertyName);
    }

    static class ObservableExtensions
    {
        public static IDisposable SubscribeTo<T, Obj>(this IObservable<T> source, Obj holder, Expression<Func<Obj, T>> propertyExpression)
            where Obj : class, IChangingProperty
        {
            System.Reflection.PropertyInfo member = null;

            var simpleExpression = propertyExpression.ReduceExtensions().Reduce();

            if (simpleExpression.NodeType == ExpressionType.Lambda)
            {
                LambdaExpression lambdaExpression = simpleExpression as LambdaExpression;

                if (lambdaExpression.Body.NodeType == ExpressionType.MemberAccess)
                {
                    MemberExpression memberExpression = lambdaExpression.Body as MemberExpression;

                    if (memberExpression.Member.MemberType == System.Reflection.MemberTypes.Property)
                    {
                        member = memberExpression.Member as System.Reflection.PropertyInfo;
                    }
                }
            }

            if (member != null)
            {
                return source.
                    ObserveOn(new System.Reactive.Concurrency.DispatcherScheduler(System.Windows.Threading.Dispatcher.CurrentDispatcher)).
                    Subscribe((val) =>
                    {
                        member.SetValue(holder, val);

                        holder.OnPropertyChanged(member.Name);
                    });
            }
            else
            {
                return null;
            }
        }
    }
}
