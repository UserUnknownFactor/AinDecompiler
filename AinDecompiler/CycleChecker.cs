using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public class CycleChecker
    {
        HashSet<Expression> seen = new HashSet<Expression>();
        public bool CheckForCycles(Expression expression)
        {
            bool retval = false;
        again:
            seen.Add(expression);
            if (expression != null && expression.Args != null)
            {
                for (int i = 0; i < expression.Args.Count; i++)
                {
                    var child = expression.Args[i];
                    if (seen.Contains(child))
                    {
                        //oh noes!  It's a cycle!
                        expression.Args[i] = null;
                        retval = true;
                    }
                    else
                    {
                        if (child != null)
                        {
                            if (i == expression.Args.Count - 1)
                            {
                                expression = child;
                                goto again;
                            }
                            CheckForCycles(child);
                        }
                    }
                }
            }
            return retval;
        }

    }
}
