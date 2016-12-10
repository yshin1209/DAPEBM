using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Interfaces;
using static Common.ServiceUriConstructor;

namespace DiabetesAPI.Controllers {

   [Route("api/[controller]")]
   public class LMSController : Controller {
      private ActorId ForwardActorId = ActorId.CreateRandom();

      [HttpGet("{mN:int}/{wN:int}/{sN}/{fN}")]
      public async Task<IActionResult> Get(int mN, int wN, double sN, double fN) {
         var actor = ActorProxy.Create<IDiabetesPatient>(ForwardActorId, ServiceUriFor("DiabetesPatient"));
         var result = await actor.LMS(mN, wN, sN, fN);
         return Ok(result);
      }
   }
}
