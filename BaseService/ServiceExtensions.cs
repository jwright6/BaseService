using System;
using System.Collections.Generic;

namespace BaseService
{
    /// <summary>
    ///     Adds extensions to be used with services
    /// </summary>
    public static class ServiceExtensions
    {
        // ReSharper disable once IdentifierTypo
        // ReSharper disable once CommentTypo
        /// <summary>
        ///     Applies an action to each element in an enumeration
        /// </summary>
        /// <typeparam name="T">Type of element</typeparam>
        /// <param name="enumerable">Enumerable type to run through</param>
        /// <param name="func">Function to apply</param>
        /// <exception cref="ArgumentNullException">The value of 'enumerable' and 'func' cannot be null. </exception>
        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        public static void Each<T>(this IEnumerable<T> enumerable, Action<T> func)
        {
            if (enumerable == null)
                throw new ArgumentNullException("enumerable");
            if (func == null)
                throw new ArgumentNullException("func");
            foreach (var i in enumerable)
                func(i);
        }

        /// <summary>
        ///     Clears a dictionary and disposes all elements
        /// </summary>
        /// <typeparam name="T">Key types in dictionary</typeparam>
        /// <typeparam name="U">Value types in dictionary</typeparam>
        /// <param name="list">List to dispose and clear</param>
        /// <param name="disposeKeys">Whether or not to dispose keys as well as values (ignored if U is not disposable)</param>
        /// <exception cref="ArgumentNullException">The value of 'list' cannot be null. </exception>
        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        /// <exception cref="NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
        public static void DisposeAll<T, U>(this IDictionary<T, U> list, bool disposeKeys = false) where U : IDisposable
        {
            if (list == null)
                throw new ArgumentNullException("list");
            if (disposeKeys && typeof (T).IsAssignableFrom(typeof (IDisposable)))
                // ReSharper disable once HeapView.SlowDelegateCreation
                list.Each(x =>
                {
                    ((IDisposable) x.Key).Dispose();
                    x.Value.Dispose();
                });
            else
            // ReSharper disable once HeapView.SlowDelegateCreation
                list.Each(x => x.Value.Dispose());
            list.Clear();
        }

        // ReSharper disable once CommentTypo
        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Clears a collection and disposes all elements
        /// </summary>
        /// <typeparam name="T">Types in enumerable</typeparam>
        /// <param name="list">List to dispose and clear</param>
        /// <exception cref="NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        /// <exception cref="ArgumentNullException">The value of 'enumerable' and 'func' cannot be null. </exception>
        public static void DisposeAll<T>(this ICollection<T> list) where T : IDisposable
        {
            // ReSharper disable once HeapView.SlowDelegateCreation
            list.Each(x => x.Dispose());
            list.Clear();
        }
    }
}