using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace sharedlib
{
    public static class Sugar
    {
        public static void Wait(Func<bool> condition, int timeoutMs)
        {
            while (condition() && timeoutMs > 0)
            {
                timeoutMs -= 10;
                Thread.Sleep(10);
            }
        }
        public static TResult With<TInput, TResult>(this TInput o,
        Func<TInput, TResult> evaluator)
            where TResult : class
            where TInput : class
        {
            if (o == null) return null;
            return evaluator(o);
        }

        public static TResult Return<TInput, TResult>(this TInput o,
        Func<TInput, TResult> evaluator, TResult failureValue) where TInput : class
        {
            if (o == null) return failureValue;
            return evaluator(o);
        }

        public static TInput If<TInput>(this TInput o, Func<TInput, bool> evaluator)
       where TInput : class
        {
            if (o == null) return null;
            return evaluator(o) ? o : null;
        }

        public static TInput Unless<TInput>(this TInput o, Func<TInput, bool> evaluator)
               where TInput : class
        {
            if (o == null) return null;
            return evaluator(o) ? null : o;
        }
        public static TInput Do<TInput>(this TInput o, Action<TInput> action)
       where TInput : class
        {
            if (o == null) return null;
            action(o);
            return o;
        }

        public static bool Try(this Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                Log.WriteInfo("Action try exception: {0}", e.Message);
                Log.WriteInfo("Stack trace:\n{0}\n{1}", e.StackTrace, e.InnerException.With(x => x.StackTrace));
                return false;
            }
        }
        public static string TryAndReport(this Action action)
        {
            try
            {
                action();
                return String.Empty;
            }
            catch (Exception e)
            {
                Log.WriteInfo("TryAndReport exception: {0}", e.Message);
                //Log.WriteInfo("Stack trace:\n{0}\n{1}", e.StackTrace, e.InnerException.With(x => x.StackTrace));
                //(App.Current as App).Do(x => x.LogException(e));
                return e.Message;
            }
        }

        public static void Try(Action action, Action<Exception> exceptionAction)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                exceptionAction(e);
            }
        }
        public static void Try<T>(this Action<T> action, T obj)
        {
            try
            {
                action(obj);
            }
            catch (Exception e)
            {
                Log.WriteInfo("Action try exception: {0}", e.Message);
            }
        }

    }
}
