using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace TrackPlanner.LinqExtensions
{
    public sealed class CompositeDisposable : IDisposable
    {
        public static IDisposable None { get; } = new CompositeDisposable(Enumerable.Empty<IDisposable>());
        
        private readonly IReadOnlyList<Action> disposables;

        public CompositeDisposable(params Action[] disposables)
        {
            this.disposables = disposables;
        }

        public CompositeDisposable(IEnumerable<IDisposable> disposables) : this(disposables.Select<IDisposable,Action>(it => it.Dispose).ToArray())
        {
        }

        public static CompositeDisposable Combine(Action a, IDisposable b)
        {
            return new CompositeDisposable(a, b.Dispose);
        }

        public void Dispose()
        {
            foreach (var disp in this.disposables)
                disp();
        }
    }
}