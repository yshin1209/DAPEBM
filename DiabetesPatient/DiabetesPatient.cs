﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using Interfaces;
using static Common.ServiceUriConstructor;
using static Common.TaskManagement;

namespace DiabetesPatient {

   [ActorService(Name = "DiabetesPatient")]
   [StatePersistence(StatePersistence.Persisted)]
   internal class DiabetesPatient : Actor, IDiabetesPatient {
      public DiabetesPatient(ActorService actorService, ActorId actorId)
          : base(actorService, actorId) {
      }

      protected override Task OnActivateAsync() {
         ActorEventSource.Current.ActorMessage(this, "Actor activated.");
         return Task.FromResult(true);
      }

      private static long measurementsNum;
      private static long weightsNum;
      private static double step;
      private static double[] filterC;
      private static double[] signals;
      private static double[] measurements;
      Task IDiabetesPatient.PrepareData(long mN, int wN, double sN, double fN) {
         /* Constants */
         measurementsNum = mN; // 1000
         weightsNum = wN; // 5
         step = sN; // 0.1
         double filterSeed = fN; // 0.5

         filterC = new double[weightsNum];
         signals = new double[measurementsNum];
         measurements = new double[measurementsNum];

         /* Filter and Signal initializations */
         for (int l = 0; l < weightsNum; l++) {
            filterC[l] = Math.Pow(filterSeed, weightsNum - l - 1);
         }

         var random = new Random();
         for (long l = 0; l < measurementsNum; l++) {
            measurements[l] = random.NextDouble();

            for (int u = 0; u < weightsNum; u++) {
               if (l - u >= 0) {
                  signals[l] += measurements[l - u] * filterC[u];
               }
            }
         }

         ActorEventSource.Current.ActorMessage(this, "Data Prepared.");
         return Task.FromResult(true);
      }

      async Task IDiabetesPatient.Initialize(long[] cluster) {
         var neighbors = new Neighbors(true);
         ActorId myActorId = this.GetActorId();
         long myId = (myActorId.Kind == ActorIdKind.Long ? myActorId.GetLongId() : myActorId.GetHashCode());

         for (int l = 0; l < cluster.Length; l++) {
            if (cluster[l] != myId) {
               neighbors[cluster[l]] = new NeighborDetails(0);
               for (long u = 0; u < weightsNum; u++) {
                  neighbors[cluster[l]][0].Add(0);
               }
            }
         }

         var weights = new List<double>();
         for (int l = 0; l < weightsNum; l++) {
            weights.Add(0);
         }

         await this.StateManager.AddOrUpdateStateAsync("neighbors", neighbors, (key, value) => neighbors);
         await this.StateManager.AddOrUpdateStateAsync<long>("currentIteration", 1, (key, value) => 1);
         await this.StateManager.AddOrUpdateStateAsync("weights", weights, (key, value) => weights);

         ActorEventSource.Current.ActorMessage(this, $"Data initialized for actor: {myId}");
         return;
      }

      async Task IDiabetesPatient.RunAStep() {
         var currentIteration = await this.StateManager.GetStateAsync<long>("currentIteration");
         var weights = await this.StateManager.GetStateAsync<List<double>>("weights");
         var neighbors = await this.StateManager.GetStateAsync<Neighbors>("neighbors");

         double[] range = new double[weightsNum];
         double[] psi = new double[weightsNum];
         double estimatedSignal = 0;
         double error = 0;

         // Input Range
         for (long l = currentIteration; l > currentIteration - weightsNum; l--) {
            if (l >= 1) {
               range[weightsNum + (l - currentIteration) - 1] = measurements[l - 1];
            } else {
               break;
            }
         }

         estimatedSignal = 0;
         for (int l = 0; l < weightsNum; l++) {
            estimatedSignal += weights[l] * range[l];
         }

         error = signals[currentIteration - 1] - estimatedSignal;

         for (int l = 0; l < weightsNum; l++) {
            psi[l] = weights[l] + (step * error * range[l]);
            weights[l] = psi[l];
         }

         for (int l = 0; l < weightsNum; l++) {
            foreach (var neighbor in neighbors) {
               weights[l] += neighbor.Value[currentIteration - 1][l];
            }
            weights[l] /= neighbors.Count + 1;
         }

         await this.StateManager.SetStateAsync("weights", weights);
         await this.StateManager.SetStateAsync("currentIteration", currentIteration + 1);

         IDiabetesPatient neighborProxy;
         ActorId myActorId = this.GetActorId();
         long myId = (myActorId.Kind == ActorIdKind.Long ? myActorId.GetLongId() : myActorId.GetHashCode());
         foreach (var neighbor in neighbors) {
            neighborProxy = ActorProxy.Create<IDiabetesPatient>(new ActorId(neighbor.Key), ServiceUriFor("DiabetesPatient"));
            neighborProxy.SendWeights(myId, currentIteration, psi).Forget();
         }

         ActorEventSource.Current.ActorMessage(this, $"Ran iteration[{currentIteration}] for actor: {myId}");
         return;
      }

      async Task IDiabetesPatient.SendWeights(long neighbor, long iterationId, double[] theirWeights) {
         var currentIteration = await this.StateManager.GetStateAsync<long>("currentIteration");
         var neighbors = await this.StateManager.GetStateAsync<Neighbors>("neighbors");

         //var message = $"Call Params: neighbor => {neighbor}, iter => {iterationId}, weights => {theirWeights.ToString()}\n";
         //foreach (var thing1 in neighbors) {
         //   message += $"A Neighbor:\n";
         //   message += $"Id: {thing1.Key}\n";
         //   message += $"Details:\n";
         //   foreach( var thing2 in thing1.Value) {
         //      message += $"Id: {thing2.Key}\n";
         //      message += $"Contents:{thing2.Value.ToString()}\n";
         //   }
         //}

         ActorEventSource.Current.ActorMessage(this, $"Call Params: neighbor => {neighbor}, iter => {iterationId}");

         neighbors[neighbor][iterationId] = new List<double>(theirWeights);
         await this.StateManager.SetStateAsync("neighbors", neighbors);

         // NOTE: (currentIteration > 1) used to prevent expanding the size of the buffer/history by 1.
         // This happens because it is possible for all actors but this to already be done with a step.
         // Therefore, all the data for the next iteration is available but we haven't finished calling
         // the initial iteration. This can happen because we are running the first iteration from
         // outside the expected flow of the application.
         if (currentIteration > 1 && currentIteration <= measurementsNum && neighbors.AllHaveDataFor(currentIteration - 1)) {
            await ((IDiabetesPatient)this).RunAStep();
         }

         return;
      }

      Task<string> IDiabetesPatient.LMS(int mN, int wN, double sN, double fN) {
         double[] msm = new double[mN];
         double[] sig = new double[mN];
         double[] rng = new double[wN];
         double[] fC = new double[wN];
         double[] wg = new double[wN];

         /* Filter and Signal initializations */
         for (int l = 0; l < wN ; l++) {
            fC[l] = Math.Pow(fN, wN - l - 1);
         }

         var random = new Random();
         for (int l = 0; l < mN; l++) {
            msm[l] = random.NextDouble();

            for (int u = 0; u < wN; u++) {
               if (l - u >= 0) {
                  sig[l] += msm[l - u] * fC[u];
               }
            }
         }

         /* LMS */
         double estimatedSignal = 0;
         double error = 0;
         for (int l = 0; l < mN; l++) {

            for (int u = l; u > l - wN; u--) {
               if (u >= 0) {
                  rng[wN + (u - l) - 1] = msm[u];
               } else {
                  break;
               }
            }

            estimatedSignal = 0;
            for (int u = 0; u < wN; u++) {
               estimatedSignal += wg[u] * rng[u];
            }

            error = sig[l] - estimatedSignal;

            for (int u = 0; u < wN; u++) {
               wg[u] = wg[u] + (sN * error * rng[u]);
            }

         }

         string message = "Comparison between weights:\n";
         for (int u = 0; u < wN; u++) {
            message += $"\t{u + 1} => {fC[wN - u - 1]} | {wg[u]}\n";
         }
         ActorEventSource.Current.ActorMessage(this, message);

         return Task.FromResult(message);
      }

   }
}
