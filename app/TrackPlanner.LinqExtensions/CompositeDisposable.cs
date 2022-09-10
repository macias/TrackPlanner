using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace TrackPlanner.LinqExtensions
{
    public sealed class CompositeDisposable : IDisposable
    {
        public static CompositeDisposable Create(IEnumerable<Action> disposables) 
        {
            return new CompositeDisposable(disposables.ToArray());
        }
        public static CompositeDisposable Create(IEnumerable<IDisposable> disposables) 
        {
            return new CompositeDisposable(toActions(disposables));
        }

        public static IDisposable None { get; } = new CompositeDisposable(Array.Empty<IDisposable>());
        
        private readonly IReadOnlyList<Action> disposables;

        public CompositeDisposable(params Action[] disposables)
        {
            this.disposables = disposables;
        }

        public CompositeDisposable(params IDisposable[] disposables) : this(toActions(disposables))
        {
        }

        private static Action[] toActions(IEnumerable<IDisposable> disposables)
        {
            return disposables.Select<IDisposable, Action>(it => it.Dispose).ToArray();
        }

        public CompositeDisposable Stack( IDisposable top)
        {
            return Stack(this, top);
        }


        public static CompositeDisposable Stack( IDisposable stack,Action top)
        {
            return new CompositeDisposable(top, stack.Dispose);
        }

        public static CompositeDisposable Stack( IDisposable stack,IDisposable top)
        {
            return new CompositeDisposable(top.Dispose, stack.Dispose);
        }

        public void Dispose()
        {
            foreach (var disp in this.disposables)
                disp();
        }
    }
}