using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using System.Runtime.Serialization;

namespace DiabetesPatient {

   [DataContract]
   public class Neighbors: IEnumerable<KeyValuePair<long, NeighborDetails>> {
      [DataMember]
      public Dictionary<long, NeighborDetails> Storage { get; set; }

      [IgnoreDataMember]
      public int Count { get { return Storage.Count; } }

      public Neighbors() { }
      public Neighbors(bool initialize) {
         Storage = new Dictionary<long, NeighborDetails>();
      }

      [IgnoreDataMember]
      public NeighborDetails this[long actorId] {
         get { return Storage[actorId]; }
         set { Storage[actorId] = value; }
      }

      [IgnoreDataMember]
      public NeighborDetails this[ActorId actorId] {
         get { return Storage[(actorId.Kind == ActorIdKind.Long ? actorId.GetLongId() : actorId.GetHashCode())]; }
         set { Storage[(actorId.Kind == ActorIdKind.Long ? actorId.GetLongId() : actorId.GetHashCode())] = value; }
      }

      public IEnumerator<KeyValuePair<long, NeighborDetails>> GetEnumerator() {
         return Storage.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
         return this.GetEnumerator();
      }

      public bool AllHaveDataFor(long iteration) {
         foreach (var neighbor in Storage) {
            if (!neighbor.Value.HasDataFor(iteration)) {
               return false;
            }
         }
         return true;
      }

   }

   [DataContract]
   public class NeighborDetails: IEnumerable<KeyValuePair<long, List<double>>> {
      [IgnoreDataMember]
      private static int MaxHistoryLength = 2; // The minimum is 2.

      [DataMember]
      public Dictionary<long, List<double>> History { get; set; }

      public NeighborDetails() { }
      public NeighborDetails(long iterationSource, List<double> weights = null) {
         History = new Dictionary<long, List<double>>();
         if (weights == null) {
            weights = new List<double>();
         }
         History[iterationSource] = weights;
      }

      [IgnoreDataMember]
      public List<double> this[long iteration] {
         get { return History[iteration]; }
         set {
            if (History.Keys.Count >= MaxHistoryLength) {
               var smallerKey = History.Keys.Min();
               if (iteration != smallerKey) {
                  History.Remove(smallerKey);
               }
            }
            History[iteration] = value;
         }
      }

      public bool HasDataFor(long iteration) {
         return History.ContainsKey(iteration);
      }

      public IEnumerator<KeyValuePair<long, List<double>>> GetEnumerator() {
         return History.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
         return this.GetEnumerator();
      }
   }

}
