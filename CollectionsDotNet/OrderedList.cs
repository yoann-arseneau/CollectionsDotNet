using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace CollectionsDotNet {
	/// <summary>How <see langword="null"/>s are treated during comparison.</summary>
	public enum NullOrdering {
		/// <summary>
		///     For reference types, the ordering of <see langword="null"/> is handled by
		///     <see cref="IComparable{T}.CompareTo(T)"/>. For value types, comparing
		///     <see langword="null"/> with a non-null value is an error.
		/// </summary>
		Natural,
		/// <summary><see langword="null"/> is considered less than non-null value.</summary>
		NullFirst,
		/// <summary><see langword="null"/> is considered greater than non-null value.</summary>
		NullLast,
	}

	public class OrderedList {
		public static OrderedList<T> FromComparable<T>(NullOrdering nullOrdering = NullOrdering.Natural)
				where T : IComparable<T> {
			var type = typeof(T);
			if (type.IsValueType) {
				return new((x, y) => x.CompareTo(y));
			}
			else {
				return nullOrdering switch {
					NullOrdering.Natural => new((x, y) => x?.CompareTo(y) ?? y?.CompareTo(x) ?? 0),
					NullOrdering.NullFirst => new((x, y) => (x, y) switch {
						(not null, not null) => x.CompareTo(y),
						(not null, null) => 1,
						(null, not null) => -1,
						(null, null) => 0,
					}),
					NullOrdering.NullLast => new((x, y) => (x, y) switch {
						(not null, not null) => x.CompareTo(y),
						(not null, null) => -1,
						(null, not null) => 1,
						(null, null) => 0,
					}),
					_ => throw new ArgumentException($"unexpected ordering '{nullOrdering}'", nameof(nullOrdering)),
				};
			}
		}
		public static OrderedList<T> FromComparable<T>(int initialCapacity, NullOrdering nullOrdering = NullOrdering.Natural)
				where T : IComparable<T> {
			if (initialCapacity < 0) {
				throw new ArgumentOutOfRangeException(nameof(initialCapacity));
			}

			var list = FromComparable<T>(nullOrdering);
			list.Capacity = initialCapacity;
			return list;
		}
		public static OrderedList<T> FromComparable<T>(IEnumerable<T> items, NullOrdering nullOrdering = NullOrdering.Natural)
				where T : IComparable<T> {
			var list = FromComparable<T>(nullOrdering);
			list.AddRange(items);
			return list;
		}

		public static OrderedList<T?> FromNullableComparable<T>(NullOrdering nullOrdering = NullOrdering.Natural)
				where T : struct, IComparable<T> {
			return nullOrdering switch {
				NullOrdering.Natural => new((x, y) => (x, y) switch {
					(not null, not null) => x.Value.CompareTo(y.Value),
					(null, not null) => throw new InvalidOperationException("cannot compare null with non-null"),
					(not null, null) => throw new InvalidOperationException("cannot compare non-null with null"),
					(null, null) => 0,
				}),
				NullOrdering.NullFirst => new((x, y) => (x, y) switch {
					(not null, not null) => x.Value.CompareTo(y.Value),
					(not null, null) => 1,
					(null, not null) => -1,
					(null, null) => 0,
				}),
				NullOrdering.NullLast => new((x, y) => (x, y) switch {
					(not null, not null) => x.Value.CompareTo(y.Value),
					(not null, null) => -1,
					(null, not null) => 1,
					(null, null) => 0,
				}),
				_ => throw new ArgumentException($"unexpected ordering '{nullOrdering}'", nameof(nullOrdering)),
			};
		}
		public static OrderedList<T?> FromNullableComparable<T>(int initialCapacity, NullOrdering nullOrdering = NullOrdering.Natural)
				where T : struct, IComparable<T> {
			if (initialCapacity < 0) {
				throw new ArgumentOutOfRangeException(nameof(initialCapacity));
			}

			var list = FromNullableComparable<T>(nullOrdering);
			list.Capacity = initialCapacity;
			return list;
		}
		public static OrderedList<T?> FromNullableComparable<T>(IEnumerable<T?> items, NullOrdering nullOrdering = NullOrdering.Natural)
				where T : struct, IComparable<T> {
			var list = FromNullableComparable<T>(nullOrdering);
			list.AddRange(items);
			return list;
		}
	}

	public class OrderedList<T> : ICollection<T>, IReadOnlyList<T>, ICollection {
		public T this[int index] {
			get {
				if (index < 0 || index >= _count) {
					throw new ArgumentOutOfRangeException(nameof(index));
				}

				return _buffer[index];
			}
		}

		public int Count => _count;
		public int Capacity {
			get => _buffer.Length;
			set {
				if (value < _buffer.Length) {
					throw new ArgumentOutOfRangeException(nameof(value));
				}
				else if (value > _buffer.Length) {
					var newBuf = new T[value];
					Array.Copy(_buffer, newBuf, _count);
					_buffer = newBuf;
				}
			}
		}
		bool ICollection<T>.IsReadOnly => false;
		bool ICollection.IsSynchronized => false;
		object ICollection.SyncRoot => throw new NotImplementedException();

		private T[] _buffer;
		private int _count;
		private readonly IComparer<T> _comparer;
		private volatile int _version;

		public OrderedList(Comparison<T> comparison)
			: this(0, Comparer<T>.Create(comparison)) {
		}
		public OrderedList(IComparer<T> comparer)
			: this(0, comparer) {
		}
		public OrderedList(int capacity, Comparison<T> comparison)
			: this(capacity, Comparer<T>.Create(comparison)) {
		}
		public OrderedList(int capacity, IComparer<T> comparer) {
			_comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
			_buffer = capacity switch {
				> 0 => new T[capacity],
				0 => Array.Empty<T>(),
				< 0 => throw new ArgumentOutOfRangeException(nameof(capacity)),
			};
		}
		public OrderedList(IEnumerable<T> collection, Comparison<T> comparer)
			: this(collection, Comparer<T>.Create(comparer)) {
		}
		public OrderedList(IEnumerable<T> collection, IComparer<T> comparer) {
			if (collection is null) {
				throw new ArgumentNullException(nameof(collection));
			}

			_comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
			if (collection is ICollection<T> col) {
				if (col.Count > 0) {
					_buffer = new T[col.Count];
					col.CopyTo(_buffer, 0);
				}
				else {
					_buffer = Array.Empty<T>();
				}
			}
			else {
				var i = 0;
				_buffer = Array.Empty<T>();
				foreach (var item in collection) {
					if (i >= _buffer.Length) {
						var newBuf = new T[Math.Max(_buffer.Length * 2, 8)];
						Array.Copy(_buffer, newBuf, _buffer.Length);
						_buffer = newBuf;
					}
					_buffer[i] = item;
				}
				_count = i;
			}
			FullSort();
		}

		public bool Contains(T item) => BinarySearch(item) >= 0;
		/// <summary>
		///     The index of a value equal to <paramref name="item"/> if at least one exists; otherwise,
		///     a negative value. See <see cref="Array.BinarySearch{T}(T[], T)"/> for more explanation.
		/// </summary>
		public int IndexOf(T item) => BinarySearch(item);

		public void Add(T item) {
			var index = BinarySearch(item);
			if (index < 0) {
				index = -index - 1;
			}
			EnsureCapacity(_count + 1);
			InsertAt(index, item);
		}
		public void AddRange(IEnumerable<T> items) {
			if (items is ICollection<T> collection && collection.Count >= 2) {
				if (collection.Count == 0) {
					return;
				}

				EnsureCapacity(_count + collection.Count);
				collection.CopyTo(_buffer, _count);
				_count += collection.Count;
				FullSort();
			}
			else {
				foreach (var item in items) {
					Add(item);
				}
			}
		}
		public void Clear() {
			_version += 1;
			Array.Clear(_buffer, 0, _count);
			_count = 0;
		}
		public bool Remove(T item) {
			var index = IndexOf(item);
			if (index is not >= 0) {
				return false;
			}

			_version += 1;
			RemoveAt(index);
			return true;
		}

		public void CopyTo(T[] array, int arrayIndex) => Array.Copy(_buffer, 0, array, arrayIndex, _count);
		public T[] ToArray() => _buffer[.._count];

		public Span<T> AsSpanUnsafe() => _buffer.AsSpan(0, _count);
		public Span<T> AsSpanUnsafe(int start) => _buffer.AsSpan(start, _count - start);
		public Span<T> AsSpanUnsafe(int start, int length) {
			if (start > _count) {
				throw new ArgumentOutOfRangeException(nameof(start));
			}
			if (start + length > _count) {
				throw new ArgumentOutOfRangeException(nameof(length));
			}

			return _buffer.AsSpan(start, length);
		}

		public IEnumerator<T> GetEnumerator() {
			var version = _version;
			var count = Volatile.Read(ref _count);
			for (var i = 0; i < count; ++i) {
				if (version != _version) {
					throw new InvalidOperationException("collection changed while enumerating");
				}
				yield return _buffer[i];
			}
		}

		private int BinarySearch(T item) => Array.BinarySearch(_buffer, 0, _count, item, _comparer);
		private void EnsureCapacity(int minimumCapacity) {
			if (_count < minimumCapacity) {
				int newSize;
				if (_count >= int.MaxValue / 2) {
					newSize = int.MaxValue;
				}
				else {
					newSize = Math.Max(Math.Max(_count * 2, minimumCapacity), 8);
				}
				var newBuf = new T[newSize];
				Array.Copy(_buffer, newBuf, _count);
				_version += 1;
				_buffer = newBuf;
			}
			else {
				_version += 1;
			}
		}
		private void FullSort() => Array.Sort(_buffer, 0, _count, _comparer);
		private void InsertAt(int index, T item) {
			Array.Copy(_buffer, index, _buffer, index + 1, _count - index);
			_buffer[index] = item;
			_count += 1;
		}
		private void RemoveAt(int index) {
			var i = index;
			for (; i < _count - 1; ++i) {
				_buffer[i] = _buffer[i + 1];
			}
			_buffer[i] = default!;
			_count -= 1;
		}

		void ICollection.CopyTo(Array array, int index) {
			Array.Copy(_buffer, 0, array, index, _count);
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
