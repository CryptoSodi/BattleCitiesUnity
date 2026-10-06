using System;
using System.Collections;
using System.Collections.Generic;

namespace BattleCities.Editor
{
    /// <summary>Deterministic fixture-only frames, without entering Play mode or sending live requests.</summary>
    internal sealed class SocialFixturePump
    {
        readonly List<Stack<IEnumerator>> routines=new List<Stack<IEnumerator>>();
        public void Start(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            routines.Add(stack);Advance(stack);
        }
        public void Run(Func<SocialFixturePump,IEnumerator> test)
        {
            try
            {
                Start(test(this));
                for(int frame=0;routines.Count>0&&frame<5000;frame++)
                {
                    var current=routines.ToArray();
                    foreach(var routine in current)if(routines.Contains(routine))Advance(routine);
                    routines.RemoveAll(routine=>routine.Count==0);
                }
                if(routines.Count>0)throw new Exception("Social fixture exceeded its frame limit.");
            }
            finally
            {
                foreach(var routine in routines.ToArray())while(routine.Count>0)(routine.Pop() as IDisposable)?.Dispose();
                routines.Clear();
            }
        }
        static void Advance(Stack<IEnumerator> stack)
        {
            while(stack.Count>0)
            {
                var top=stack.Peek();
                if(!top.MoveNext()){stack.Pop();(top as IDisposable)?.Dispose();continue;}
                if(top.Current is IEnumerator nested){stack.Push(nested);continue;}
                if(top.Current!=null)throw new Exception("Unsupported yield in edit-mode social fixture: "+top.Current.GetType().Name);
                return;
            }
        }
    }
}