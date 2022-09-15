using System.Linq;
using Xunit;
using TrackPlanner.Structures;

namespace TrackPlanner.Tests
{
    public class IdMapTest
    {
        [Fact]
        public void AdditionsTest()
        {
            {
                var map = new IdMap<string>();
                map.Add(1, "ello");
                map.Add(0, "h");
                map.Add(3, "world");
                map.Add(5, "!");

                var result = map.ToList();
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

                result = map.ToList();
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
                var map = new IdMap<string>();
                Assert.True( map.TryAdd(1, "ello",out _));
                Assert.True(map.TryAdd(0, "h", out _));
                Assert.True(map.TryAdd(3, "world", out _));
                Assert.True(map.TryAdd(5, "!", out _));

                var result = map.ToList();
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

                result = map.ToList();
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