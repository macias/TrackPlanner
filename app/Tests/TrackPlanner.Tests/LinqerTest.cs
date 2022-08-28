using System;
using System.Linq;
using FluentAssertions;
using TrackPlanner.Turner;
using Xunit;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Tests
{
    public class LinqerTest
    {
        [Fact]
        public void SlideTest()
        {
            {
                var result = new[] {1}.Slide().ToArray();
                result.Should().Equal(Array.Empty<(int, int)>());
            }
            {
                var result = new[] {1,2}.Slide().ToArray();
                result.Should().Equal(new []{(1,2)});
            }
            {
                var result = new[] {1,2,3}.Slide().ToArray();
                result.Should().Equal(new []{(1,2),(2,3)});
            }
            {
                var result = new[] {1, 2, 3, 4}.Slide().ToArray();
                result.Should().Equal(new[] {(1, 2), (2, 3), (3, 4)});
            }
        }
        [Fact]
        public void ConsecutiveDistinctTest()
        {
            {
                var result = new int[0].ConsecutiveDistinct().ToList();
                Assert.Equal(0, result.Count);
            }
            {
                var result = new int[] { 7 }.ConsecutiveDistinct().ToList();
                Assert.Equal(1, result.Count);
            }
            {
                var result = new int[] { 7, 3 }.ConsecutiveDistinct().ToList();
                Assert.Equal(2, result.Count);

                Assert.Equal(7, result[0]);
                Assert.Equal(3, result[1]);
            }
            {
                var result = new int[] { 7, 3, 3 }.ConsecutiveDistinct().ToList();
                Assert.Equal(2, result.Count);

                Assert.Equal(7, result[0]);
                Assert.Equal(3, result[1]);
            }
            {
                var result = new int[] { 7, 7, 3, }.ConsecutiveDistinct().ToList();
                Assert.Equal(2, result.Count);

                Assert.Equal(7, result[0]);
                Assert.Equal(3, result[1]);
            }
            {
                var result = new int[] { 7, 3, 3, 7 }.ConsecutiveDistinct().ToList();
                Assert.Equal(3, result.Count);

                Assert.Equal(7, result[0]);
                Assert.Equal(3, result[1]);
                Assert.Equal(7, result[2]);
            }
            {
                var result = new int[] { 3, 7, 7, 3 }.ConsecutiveDistinct().ToList();
                Assert.Equal(3, result.Count);

                Assert.Equal(3, result[0]);
                Assert.Equal(7, result[1]);
                Assert.Equal(3, result[2]);
            }
        }
    }
}