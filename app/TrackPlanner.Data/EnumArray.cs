using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TrackPlanner.Data
{
    // good thread: https://stackoverflow.com/questions/16960555/how-do-i-cast-a-generic-enum-to-int
    public sealed class EnumArray<TEnum, TValue>: IEnumerable<TValue>
        where TEnum : struct, System.Enum
    {
        private readonly TValue[] data;

        public TValue this[TEnum index]
        {
            get { return data[(int) (object) index]; }
            set { data[(int) (object) index] = value; }
        }

        public EnumArray()
        {
            this.data = new TValue[Enum.GetNames<TEnum>().Length];
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return this.data.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
