namespace UltimaOnline
{
    public class KeywordList
    {
        int[] _Keywords;

        public KeywordList()
        {
            _Keywords = new int[8];
            Count = 0;
        }

        public int Count { get; private set; }

        public bool Contains(int keyword)
        {
            var contains = false;
            for (var i = 0; !contains && i < Count; ++i)
                contains = keyword == _Keywords[i];
            return contains;
        }

        public void Add(int keyword)
        {
            if ((Count + 1) > _Keywords.Length)
            {
                var old = _Keywords;
                _Keywords = new int[old.Length * 2];
                for (var i = 0; i < old.Length; ++i)
                    _Keywords[i] = old[i];
            }
            _Keywords[Count++] = keyword;
        }

        static int[] _EmptyInts = new int[0];

        public int[] ToArray()
        {
            if (Count == 0)
                return _EmptyInts;
            var keywords = new int[Count];
            for (var i = 0; i < Count; ++i)
                keywords[i] = _Keywords[i];
            Count = 0;
            return keywords;
        }
    }
}