using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace TrackPlanner.Mapping
{
    public sealed class CompositeDisposable : IDisposable
    {
        public static IDisposable None { get; } = new CompositeDisposable(Enumerable.Empty<IDisposable>());
        
        private readonly IReadOnlyList<Action> disposables;

        private CompositeDisposable(params Action[] reversedDisposables)
        {
            this.disposables = reversedDisposables;
        }

        public CompositeDisposable(IEnumerable<IDisposable> disposables) : this(disposables.Reverse().Select<IDisposable,Action>(it => it.Dispose).ToArray())
        {
        }

        public static CompositeDisposable Combine(IDisposable a, Action b)
        {
            return new CompositeDisposable(() =>
            {
                b();
                a.Dispose();
            });
        }

        public void Dispose()
        {
            foreach (var disp in this.disposables)
                disp();
        }
    }
}