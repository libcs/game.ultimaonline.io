namespace UltimaOnline
{
    public class Point3DList
    {
        private Point3D[] _List;

        public Point3DList()
        {
            _List = new Point3D[8];
            Count = 0;
        }

        public int Count { get; private set; }

        public void Clear()
        {
            Count = 0;
        }

        public Point3D Last => _List[Count - 1];

        public Point3D this[int index] => _List[index];

        public void Add(int x, int y, int z)
        {
            if ((Count + 1) > _List.Length)
            {
                var old = _List;
                _List = new Point3D[old.Length * 2];
                for (var i = 0; i < old.Length; ++i)
                    _List[i] = old[i];
            }
            _List[Count].X = x;
            _List[Count].Y = y;
            _List[Count].Z = z;
            ++Count;
        }

        public void Add(Point3D p)
        {
            if ((Count + 1) > _List.Length)
            {
                var old = _List;
                _List = new Point3D[old.Length * 2];
                for (var i = 0; i < old.Length; ++i)
                    _List[i] = old[i];
            }
            _List[Count].X = p.X;
            _List[Count].Y = p.Y;
            _List[Count].Z = p.Z;
            ++Count;
        }

        static Point3D[] _EmptyList = new Point3D[0];

        public Point3D[] ToArray()
        {
            if (Count == 0)
                return _EmptyList;
            var list = new Point3D[Count];
            for (var i = 0; i < Count; ++i)
                list[i] = _List[i];
            Count = 0;
            return list;
        }
    }
}