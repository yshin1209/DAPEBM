using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Interfaces;
using static Common.ServiceUriConstructor;
using static Common.TaskManagement;
using System.Text;

namespace DiabetesAPI.Controllers {

   [Route("api")]
   public class LMSController : Controller {
      private ActorId ForwardActorId = ActorId.CreateRandom();

      [HttpGet("[action]/{mN:long}/{wN:int}/{sN}/{fN}/{nN:int}")]
      public async Task<IActionResult> DLMS(long mN, int wN, double sN, double fN, int nN) {
         var result = "Can't run";

         if (nN > 1) {
            IDiabetesPatient actor;
            var initialActor = (new Random()).Next(0, 999999);

            actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(initialActor), ServiceUriFor("DiabetesPatient"));
            await actor.PrepareData(mN, wN, sN, fN);

            var cluster = new long[nN];
            for (var l = 0; l < cluster.Length; l++) {
               cluster[l] = initialActor + l;
            }

            for (var l = 0; l < cluster.Length; l++) {
               actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(cluster[l]), ServiceUriFor("DiabetesPatient"));
               await actor.Initialize(cluster);
            }

            for (var l = 0; l < cluster.Length; l++) {
               actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(cluster[l]), ServiceUriFor("DiabetesPatient"));
               actor.RunAStep(null).Forget();
            }

            result = "It's running.";
         }
         
         return Ok(result);
      }

      [HttpGet("[action]/{mN:int}/{wN:int}/{sN}/{fN}")]
      public async Task<IActionResult> LMS(int mN, int wN, double sN, double fN) {
         var actor = ActorProxy.Create<IDiabetesPatient>(ForwardActorId, ServiceUriFor("DiabetesPatient"));
         var result = await actor.LMS(mN, wN, sN, fN);
         return Ok(result);
      }
   }
}
