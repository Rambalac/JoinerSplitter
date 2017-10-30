namespace JoinerSplitter
{
    using System.Collections.Generic;

    public static class CollectionExtensions
    {
        public static IEnumerable<IList<TItem>> SelectGroups<TItem>(this IEnumerable<TItem> items, int groupSize)
        {
            var curlist = new List<TItem>(groupSize);
            foreach (var item in items)
            {
                curlist.Add(item);
                if (curlist.Count == groupSize)
                {
                    yield return curlist;
                    curlist = new List<TItem>(groupSize);
                }
            }

            if (curlist.Count > 0)
            {
                yield return curlist;
            }
        }
    }
}