// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)



using System;
using System.Collections.Generic;
using System.Text;


namespace QUT.GPGen
{
	public class SetCollection<T>: IEnumerable<T>
	{
		private Dictionary<T, bool> elements = new Dictionary<T, bool>();

		public SetCollection()
		{
		}


		public SetCollection(SetCollection<T> items)
		{
			AddRange(items);
		}


		public void Add(T item)
		{
			elements[item] = true;
		}


		public void AddRange(SetCollection<T> items)
		{
			foreach (T item in items)
				Add(item);
		}


		public IEnumerator<T> GetEnumerator()
		{
			return elements.Keys.GetEnumerator();
		}


		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException("The method or operation is not implemented.");
		}


		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();

			builder.Append("[");

			foreach (T element in elements.Keys)
				builder.AppendFormat("{0}, ", element);

			builder.Append("]");

			return builder.ToString();
		}
	}
}