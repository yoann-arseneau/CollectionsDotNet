using CollectionsDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectionsDotNetTests {
	[TestClass]
	public class OrderedListTests {
		const int Length = 100;

		readonly Random rand = new();

		[TestMethod]
		public void OrderedListTests_CreationTests() {
			ctor((x, y) => x.CompareTo(y), Comparer<string>.Default);
			ctor((x, y) => x.CompareTo(y), Comparer<int>.Default);
			ctor((x, y) => x!.Value.CompareTo(y!.Value), Comparer<int?>.Default);

			fromComparable<string>();
			fromComparable<int>();
			fromNullableComparable<int>();

			static void ctor<T>(Comparison<T> comparison, Comparer<T> comparer) {
				_ = new OrderedList<T>(comparison);
				_ = new OrderedList<T>(0, comparison);
				_ = new OrderedList<T>(Enumerable.Empty<T>(), comparison);
				_ = new OrderedList<T>(comparer);
				_ = new OrderedList<T>(0, comparer);
				_ = new OrderedList<T>(Enumerable.Empty<T>(), comparer);
			}
			static void fromComparable<T>() where T : IComparable<T> {
				OrderedList.FromComparable<T>();
				OrderedList.FromComparable<T>(0);
				OrderedList.FromComparable<T>(Enumerable.Empty<T>());
			}
			static void fromNullableComparable<T>() where T : struct, IComparable<T> {
				OrderedList.FromNullableComparable<T>();
				OrderedList.FromNullableComparable<T>(0);
				OrderedList.FromNullableComparable<T>(Enumerable.Empty<T?>());
			}
		}

		[TestMethod]
		public void OrderedListTests_AllAddsAreEquivalent() {
			List<double> expected = new(Length);

			// using Add
			var add = OrderedList.FromComparable<double>();
			for (var i = 0; i < Length; ++i) {
				var v = rand.NextDouble();
				expected.Add(v);
				add.Add(v);
			}

			// using AddRange and ICollection optimization
			var addCollection = OrderedList.FromComparable<double>();
			addCollection.AddRange(expected);

			// using AddRange without ICollection optimization
			var addEnumerable = OrderedList.FromComparable<double>();
			addEnumerable.AddRange(expected.Select(x => x));

			expected.Sort();
			CollectionAssert.AreEqual(expected, add);
			CollectionAssert.AreEqual(expected, addCollection);
			CollectionAssert.AreEqual(expected, addEnumerable);
		}

		[TestMethod]
		public void OrderedListTests_AddAndRemove() {
			List<double> expected = new(Length * 2);
			var actualFrom = OrderedList.FromComparable<double>();
			var actualCtor = new OrderedList<double>((x, y) => x.CompareTo(y));
			for (var i = 0; i < 50; ++i) {
				switch (i % 5) {
					case 0: {
						var val = rand.NextDouble();
						expected.Add(val);
						actualFrom.Add(val);
						actualCtor.Add(val);
						break;
					}
					case 1: {
						var v0 = rand.NextDouble();
						var v1 = rand.NextDouble();
						expected.Add(v0);
						expected.Add(v1);
						actualFrom.AddRange(new[] { v0, v1 });
						actualCtor.AddRange(new[] { v0, v1 });
						break;
					}
					case 2: {
						var v0 = rand.NextDouble();
						var v1 = rand.NextDouble();
						expected.Add(v0);
						expected.Add(v1);
						actualFrom.AddRange(new[] { v0, v1 }.Select(x => x));
						actualCtor.AddRange(new[] { v0, v1 }.Select(x => x));
						break;
					}
					case 3: {
						// remove actual item
						var val = expected[rand.Next(expected.Count)];
						expected.Remove(val);
						actualFrom.Remove(val);
						actualCtor.Remove(val);
						break;
					}
					case 4: {
						// most likely failed remove
						var val = rand.NextDouble();
						expected.Remove(val);
						actualFrom.Remove(val);
						actualCtor.Remove(val);
						break;
					}
				}
			}
			expected.Sort();
			CollectionAssert.AreEqual(expected, actualFrom);
			CollectionAssert.AreEqual(expected, actualCtor);
		}

		[TestMethod]
		public void OrderedListTests_NullOrderingForNullable() {
			var list = OrderedList.FromNullableComparable<int>(NullOrdering.Natural);
			list.Add(null);
			Assert.ThrowsException<InvalidOperationException>(() => list.Add(6));
			list.Add(null);
			Assert.ThrowsException<InvalidOperationException>(() => list.Add(5));

			list = OrderedList.FromNullableComparable<int>(NullOrdering.NullFirst);
			list.AddRange(new int?[] { null, 6, null, 5 });
			CollectionAssert.AreEqual(new int?[] { null, null, 5, 6 }, list);

			list = OrderedList.FromNullableComparable<int>(NullOrdering.NullLast);
			list.AddRange(new int?[] { null, 6, null, 5 });
			CollectionAssert.AreEqual(new int?[] { 5, 6, null, null }, list);
		}
	}
}
