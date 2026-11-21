using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fonts.Metadata.Internal {
    internal sealed class ResultList<T>(IList<T> items) : ReadOnlyCollection<T>(items), ICollection {
        // ReadOnlyCollection forwards SyncRoot to its backing list or array.
        object ICollection.SyncRoot { get { return this; } }
        internal static ReadOnlyCollection<T> Snapshot(List<T> items) {
            if(items.Count == 0)
                return ReadOnlyCollection<T>.Empty;
            return new ResultList<T>(items.ToArray());
        }
    }
}
