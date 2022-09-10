using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Turner;
using Xunit;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Storage.Data;

namespace TrackPlanner.Tests
{
    public class CompactDictionaryTest
    {
        [Fact]
        public void LongResizingTest()
        {
            // 200 --> pure hash 198
            // 23 --> pure hash 21
            var input = new long[] { 200,23, 2,3,5,7,11,13,17,19,111, 4, 6, 8, 12, 202, 444,401};
            var dict = new CompactDictionaryFirst<long,long>( );
            foreach (var x in input)
            {
                long y;
                long big_x =x + int.MaxValue;
                Console.WriteLine($"ADDING {x} as key {big_x}");
                if (x == 23)
                {
                    dict.Expand();
                    Console.WriteLine("Expanded");
                    dict.DEBUG_DUMP();
                }

                dict.Add(big_x,-x);
                dict.DEBUG_DUMP();
                Assert.True(dict.TryGetValue(big_x,out y));
                Assert.Equal(-x,y);
                dict.TrimExcess();
                dict.DEBUG_DUMP();
                Assert.True(dict.TryGetValue(big_x,out y));
                Assert.Equal(-x,y);
            }
        }

        [Fact]
        public void LongAdditionsTest()
        {
            
                var map = new CompactDictionaryFirst<long,string>( );
                map.Add(1L+int.MaxValue, "ello");
                map.DEBUG_DUMP();
                map.Add(0L+int.MaxValue, "h");
                map.DEBUG_DUMP();
                map.Add(3L+int.MaxValue, "world");
                map.Add(5L+int.MaxValue, "!");

                var result = map.OrderBy(it => it.Key).ToList();
                int i;
                i = 0;
                Assert.Equal(0L+int.MaxValue, result[i++].Key);
                Assert.Equal(1L+int.MaxValue, result[i++].Key);
                Assert.Equal(3L+int.MaxValue, result[i++].Key);
                Assert.Equal(5L+int.MaxValue, result[i++].Key);

                i = 0;
                Assert.Equal("h", result[i++].Value);
                Assert.Equal("ello", result[i++].Value);
                Assert.Equal("world", result[i++].Value);
                Assert.Equal("!", result[i++].Value);

                Assert.True(map.ContainsKey(1L+int.MaxValue));
                Assert.False(map.ContainsKey(2L+int.MaxValue));

                map[-1] = "foo";

                result = map.OrderBy(it => it.Key).ToList();
                i = 0;
                Assert.Equal(-1, result[i++].Key);
                Assert.Equal(0L+int.MaxValue, result[i++].Key);
                Assert.Equal(1L+int.MaxValue, result[i++].Key);
                Assert.Equal(3L+int.MaxValue, result[i++].Key);
                Assert.Equal(5L+int.MaxValue, result[i++].Key);

                i = 0;
                Assert.Equal("foo", result[i++].Value);
                Assert.Equal("h", result[i++].Value);
                Assert.Equal("ello", result[i++].Value);
                Assert.Equal("world", result[i++].Value);
                Assert.Equal("!", result[i++].Value);
            
        }

        [Fact]
        public void AdditionsTest()
        {
            {
                var map = new CompactDictionaryFirst<long,string>( );
                map.Add(1, "ello");
                map.Add(0, "h");
                map.Add(3, "world");
                map.Add(5, "!");

                var result = map.OrderBy(it => it.Key).ToList();
                int i;
                i = 0;
                Assert.Equal(0, result[i++].Key);
                Assert.Equal(1, result[i++].Key);
                Assert.Equal(3, result[i++].Key);
                Assert.Equal(5, result[i++].Key);

                i = 0;
                Assert.Equal("h", result[i++].Value);
                Assert.Equal("ello", result[i++].Value);
                Assert.Equal("world", result[i++].Value);
                Assert.Equal("!", result[i++].Value);

                Assert.True(map.ContainsKey(1));
                Assert.False(map.ContainsKey(2));

                map[-1] = "foo";

                result = map.OrderBy(it => it.Key).ToList();
                i = 0;
                Assert.Equal(-1, result[i++].Key);
                Assert.Equal(0, result[i++].Key);
                Assert.Equal(1, result[i++].Key);
                Assert.Equal(3, result[i++].Key);
                Assert.Equal(5, result[i++].Key);

                i = 0;
                Assert.Equal("foo", result[i++].Value);
                Assert.Equal("h", result[i++].Value);
                Assert.Equal("ello", result[i++].Value);
                Assert.Equal("world", result[i++].Value);
                Assert.Equal("!", result[i++].Value);
            }
        }

        [Fact]
        public void TryAdditionsTest()
        {
            {
                var map = new CompactDictionaryFirst<long,string>();
                Assert.True( map.TryAdd(1, "ello",out _));
                Assert.True(map.TryAdd(0, "h", out _));
                Assert.True(map.TryAdd(3, "world", out _));
                Assert.True(map.TryAdd(5, "!", out _));

                var result = map.OrderBy(it => it.Key).ToList();
                int i;
                i = 0;
                Assert.Equal(0, result[i++].Key);
                Assert.Equal(1, result[i++].Key);
                Assert.Equal(3, result[i++].Key);
                Assert.Equal(5, result[i++].Key);

                i = 0;
                Assert.Equal("h", result[i++].Value);
                Assert.Equal("ello", result[i++].Value);
                Assert.Equal("world", result[i++].Value);
                Assert.Equal("!", result[i++].Value);

                Assert.True(map.ContainsKey(1));
                Assert.False(map.ContainsKey(2));

                map[-1] = "foo";

                result = map.OrderBy(it => it.Key).ToList();
                i = 0;
                Assert.Equal(-1, result[i++].Key);
                Assert.Equal(0, result[i++].Key);
                Assert.Equal(1, result[i++].Key);
                Assert.Equal(3, result[i++].Key);
                Assert.Equal(5, result[i++].Key);

                i = 0;
                Assert.Equal("foo", result[i++].Value);
                Assert.Equal("h", result[i++].Value);
                Assert.Equal("ello", result[i++].Value);
                Assert.Equal("world", result[i++].Value);
                Assert.Equal("!", result[i++].Value);
            }
        }
    }
}