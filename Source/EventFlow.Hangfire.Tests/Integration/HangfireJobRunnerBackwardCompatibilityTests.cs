// The MIT License (MIT)
// 
// Copyright (c) 2015-2025 Rasmus Mikkelsen
// https://github.com/eventflow/EventFlow
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Threading;
using System.Threading.Tasks;
using EventFlow.Hangfire.Integration;
using EventFlow.Jobs;
using NUnit.Framework;
using Shouldly;

namespace EventFlow.Hangfire.Tests.Integration
{
    /// <summary>
    /// Exercises the legacy Hangfire job runner signatures removed in EventFlow 1.x to ensure
    /// that jobs enqueued by EventFlow.Hangfire 0.x still deserialize and execute via the
    /// forwarding overloads introduced to address issue #1109.
    /// </summary>
    [TestFixture]
    public class HangfireJobRunnerBackwardCompatibilityTests
    {
        private sealed class RecordingJobRunner : IJobRunner
        {
            public (string JobName, int Version, string Job, CancellationToken CancellationToken)? LastCall { get; private set; }

            public Task ExecuteAsync(string jobName, int version, string json, CancellationToken cancellationToken)
            {
                LastCall = (jobName, version, json, cancellationToken);
                return Task.CompletedTask;
            }

            public void Reset()
            {
                LastCall = null;
            }
        }

        [Test]
        public void OldJobRunnerSignatureIsStillExposed()
        {
            var methodInfo = typeof(IHangfireJobRunner).GetMethod(
                "ExecuteAsync",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(string),
                    typeof(string),
                });

            methodInfo.ShouldNotBeNull();
        }

        [Test]
        public async Task OldSignatureDelegatesToModernImplementation()
        {
            var recordingJobRunner = new RecordingJobRunner();
            var hangfireJobRunner = new HangfireJobRunner(recordingJobRunner);

            #pragma warning disable CS0618
            await hangfireJobRunner.ExecuteAsync("display", "job", 7, "payload").ConfigureAwait(false);

            recordingJobRunner.LastCall.ShouldNotBeNull();
            recordingJobRunner.LastCall.Value.JobName.ShouldBe("job");
            recordingJobRunner.LastCall.Value.Version.ShouldBe(7);
            recordingJobRunner.LastCall.Value.Job.ShouldBe("payload");
            recordingJobRunner.LastCall.Value.CancellationToken.ShouldBe(CancellationToken.None);

            recordingJobRunner.Reset();

            await hangfireJobRunner.ExecuteAsync("display", "job", 7, "payload", "queue").ConfigureAwait(false);
            #pragma warning restore CS0618

            recordingJobRunner.LastCall.ShouldNotBeNull();
            recordingJobRunner.LastCall.Value.JobName.ShouldBe("job");
            recordingJobRunner.LastCall.Value.Version.ShouldBe(7);
            recordingJobRunner.LastCall.Value.Job.ShouldBe("payload");
        }
    }
}
