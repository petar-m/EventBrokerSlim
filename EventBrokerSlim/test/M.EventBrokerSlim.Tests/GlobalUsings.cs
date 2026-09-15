global using System;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using M.EventBrokerSlim.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection;
global using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: Parallelization(Mode = ParallelMode.None)]
