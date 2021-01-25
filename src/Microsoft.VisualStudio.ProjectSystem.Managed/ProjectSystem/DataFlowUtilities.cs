// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    ///     Provides common well-known project flags.
    /// </summary>
    internal static class DataflowUtilities
    {
        private const long CacheVersion = 0L;

        public static IComparable CacheModeVersion => CacheVersion;

        /// <summary>
        ///     Links to the specified <see cref="Action{T}" /> to receive a cross-sectional slice of project
        ///     data,  including detailed descriptions of what changed between snapshots, as described by
        ///     specified rules.
        /// </summary>
        /// <param name="source">
        ///     The broadcasting block that produces the messages.
        /// </param>
        /// <param name="target">
        ///     The <see cref="Action{T}"/> to receive the broadcasts.
        /// </param>
        /// <param name="project">
        ///     The project related to the failure, if applicable.
        /// </param>
        /// <param name="severity">
        ///     The severity of any failure that occurs.
        /// </param>
        /// <param name="suppressVersionOnlyUpdates">
        ///    A value indicating whether to prevent messages from propagating to the target
        ///     block if no project changes are include other than an incremented version number.
        /// </param>
        /// <param name="ruleNames">
        ///     The names of the rules that describe the project data the caller is interested in.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="target"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="project"/> is <see langword="null"/>.
        /// </exception>
        public static IDisposable LinkToAction(
            this ISourceBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>> source,
            Action<IProjectVersionedValue<IProjectSubscriptionUpdate>> target,
            UnconfiguredProject project,
            ProjectFaultSeverity severity = ProjectFaultSeverity.Recoverable,
            bool suppressVersionOnlyUpdates = true,
            params string[] ruleNames)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(target, nameof(target));
            Requires.NotNull(project, nameof(project));

            return source.LinkTo(DataflowBlockFactory.CreateActionBlock(target, project, severity),
                                 DataflowOption.PropagateCompletion,
                                 initialDataAsNew: true,
                                 suppressVersionOnlyUpdates: suppressVersionOnlyUpdates,
                                 ruleNames: ruleNames);
        }

        /// <summary>
        ///     Links to the specified <see cref="Action{T}" /> to receive a cross-sectional slice of project
        ///     data,  including detailed descriptions of what changed between snapshots, as described by
        ///     specified rules.
        /// </summary>
        /// <param name="source">
        ///     The broadcasting block that produces the messages.
        /// </param>
        /// <param name="target">
        ///     The <see cref="Action{T}"/> to receive the broadcasts.
        /// </param>
        /// <param name="project">
        ///     The project related to the failure, if applicable.
        /// </param>
        /// <param name="severity">
        ///     The severity of any failure that occurs.
        /// </param>
        /// <param name="suppressVersionOnlyUpdates">
        ///    A value indicating whether to prevent messages from propagating to the target
        ///     block if no project changes are include other than an incremented version number.
        /// </param>
        /// <param name="ruleNames">
        ///     The names of the rules that describe the project data the caller is interested in.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="target"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="project"/> is <see langword="null"/>.
        /// </exception>
        public static IDisposable LinkToAction(
            this ISourceBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>> source,
            Action<IProjectVersionedValue<IProjectSubscriptionUpdate>> target,
            UnconfiguredProject project,
            ProjectFaultSeverity severity = ProjectFaultSeverity.Recoverable,
            bool suppressVersionOnlyUpdates = true,
            IEnumerable<string>? ruleNames = null)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(target, nameof(target));
            Requires.NotNull(project, nameof(project));

            return source.LinkTo(DataflowBlockFactory.CreateActionBlock(target, project, severity),
                                 DataflowOption.PropagateCompletion,
                                 initialDataAsNew: true,
                                 suppressVersionOnlyUpdates: suppressVersionOnlyUpdates,
                                 ruleNames: ruleNames);
        }

        /// <summary>
        ///     Links to the specified <see cref="Func{T, TResult}" /> to receive a cross-sectional slice of project
        ///     data,  including detailed descriptions of what changed between snapshots, as described by
        ///     specified rules.
        /// </summary>
        /// <param name="source">
        ///     The broadcasting block that produces the messages.
        /// </param>
        /// <param name="target">
        ///     The <see cref="Action{T}"/> to receive the broadcasts.
        /// </param>
        /// <param name="project">
        ///     The project related to the failure, if applicable.
        /// </param>
        /// <param name="suppressVersionOnlyUpdates">
        ///    A value indicating whether to prevent messages from propagating to the target
        ///     block if no project changes are include other than an incremented version number.
        /// </param>
        /// <param name="ruleNames">
        ///     The names of the rules that describe the project data the caller is interested in.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="target"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="project"/> is <see langword="null"/>.
        /// </exception>
        public static IDisposable LinkToAsyncAction(
            this ISourceBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>> source,
            Func<IProjectVersionedValue<IProjectSubscriptionUpdate>, Task> target,
            UnconfiguredProject project,
            bool suppressVersionOnlyUpdates = true,
            params string[] ruleNames)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(target, nameof(target));
            Requires.NotNull(project, nameof(project));

            return source.LinkTo(DataflowBlockFactory.CreateActionBlock(target, project, ProjectFaultSeverity.Recoverable),
                                 DataflowOption.PropagateCompletion,
                                 initialDataAsNew: true,
                                 suppressVersionOnlyUpdates: suppressVersionOnlyUpdates,
                                 ruleNames: ruleNames);
        }

        /// <summary>
        ///     Links to the specified <see cref="Func{T, TResult}" /> to receive a cross-sectional slice of project
        ///     data,  including detailed descriptions of what changed between snapshots, as described by
        ///     specified rules.
        /// </summary>
        /// <param name="source">
        ///     The broadcasting block that produces the messages.
        /// </param>
        /// <param name="target">
        ///     The <see cref="Action{T}"/> to receive the broadcasts.
        /// </param>
        /// <param name="project">
        ///     The project related to the failure, if applicable.
        /// </param>
        /// <param name="severity">
        ///     The severity of any failure that occurs.
        /// </param>
        /// <param name="suppressVersionOnlyUpdates">
        ///    A value indicating whether to prevent messages from propagating to the target
        ///     block if no project changes are include other than an incremented version number.
        /// </param>
        /// <param name="ruleNames">
        ///     The names of the rules that describe the project data the caller is interested in.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="target"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="project"/> is <see langword="null"/>.
        /// </exception>
        public static IDisposable LinkToAsyncAction(
            this ISourceBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>> source,
            Func<IProjectVersionedValue<IProjectSubscriptionUpdate>, Task> target,
            UnconfiguredProject project,
            ProjectFaultSeverity severity = ProjectFaultSeverity.Recoverable,
            bool suppressVersionOnlyUpdates = true,
            IEnumerable<string>? ruleNames = null)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(target, nameof(target));
            Requires.NotNull(project, nameof(project));

            return source.LinkTo(DataflowBlockFactory.CreateActionBlock(target, project, severity),
                                 DataflowOption.PropagateCompletion,
                                 initialDataAsNew: true,
                                 suppressVersionOnlyUpdates: suppressVersionOnlyUpdates,
                                 ruleNames: ruleNames);
        }

        /// <summary>
        ///     Links the <see cref="ISourceBlock{TOutput}" /> to the specified <see cref="Action{T}" />
        ///     that can process messages, propagating completion and faults.
        /// </summary>
        /// <param name="source">
        ///     The broadcasting block that produces the messages.
        /// </param>
        /// <param name="target">
        ///     The <see cref="Action{T}"/> to receive the broadcasts.
        /// </param>
        /// <param name="project">
        ///     The project related to the failure, if applicable.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> that, upon calling Dispose, will unlink the source from the target.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="target"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="project"/> is <see langword="null"/>.
        /// </exception>
        public static IDisposable LinkToAction<T>(
            this ISourceBlock<T> source,
            Action<T> target,
            UnconfiguredProject project)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(target, nameof(target));
            Requires.NotNull(project, nameof(project));

            return source.LinkTo(DataflowBlockFactory.CreateActionBlock(target, project, ProjectFaultSeverity.Recoverable),
                                 DataflowOption.PropagateCompletion);
        }

        /// <summary>
        ///     Links the <see cref="ISourceBlock{TOutput}" /> to the specified <see cref="Func{T, TResult}" />
        ///     that can process messages, propagating completion and faults.
        /// </summary>
        /// <returns>
        ///     An <see cref="IDisposable"/> that, upon calling Dispose, will unlink the source from the target.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="target"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="project"/> is <see langword="null"/>.
        /// </exception>
        public static IDisposable LinkToAsyncAction<T>(
            this ISourceBlock<T> source,
            Func<T, Task> target,
            UnconfiguredProject project)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(target, nameof(target));
            Requires.NotNull(project, nameof(project));

            return source.LinkTo(DataflowBlockFactory.CreateActionBlock(target, project, ProjectFaultSeverity.Recoverable),
                                 DataflowOption.PropagateCompletion);
        }

        /// <summary>
        ///     Creates a source block that produces a transformed value for each value from original source block.
        /// </summary>
        /// <typeparam name="TInput">
        ///     The type of the input value produced by <paramref name="source"/>.
        /// </typeparam>
        /// <typeparam name="TOut">
        ///     The type of value produced by <paramref name="transform"/>.
        ///  </typeparam>
        /// <param name="source">
        ///     The source block whose values are to be transformed.
        /// </param>
        /// <param name="transform">
        ///     The function to execute on each value from <paramref name="source"/>.
        /// </param>
        /// <returns>
        ///     The transformed source block and a disposable value that terminates the link.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="transform"/> is <see langword="null"/>.
        /// </exception>
        public static DisposableValue<ISourceBlock<TOut>> Transform<TInput, TOut>(
            this ISourceBlock<IProjectVersionedValue<TInput>> source,
            Func<IProjectVersionedValue<TInput>, TOut> transform)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(transform, nameof(transform));

            IPropagatorBlock<IProjectVersionedValue<TInput>, TOut> transformBlock = DataflowBlockSlim.CreateTransformBlock(transform);

            IDisposable link = source.LinkTo(transformBlock, DataflowOption.PropagateCompletion);

            return new DisposableValue<ISourceBlock<TOut>>(transformBlock, link);
        }

        /// <summary>
        ///     Creates a source block that produces multiple transformed values for each value from original source block,
        ///     skipping intermediate input and output states, and hence is not suitable for producing or consuming
        ///     deltas.
        /// </summary>
        /// <typeparam name="TInput">
        ///     The type of the input value produced by <paramref name="source"/>.
        /// </typeparam>
        /// <typeparam name="TOut">
        ///     The type of value produced by <paramref name="transform"/>.
        ///  </typeparam>
        /// <param name="source">
        ///     The source block whose values are to be transformed.
        /// </param>
        /// <param name="transform">
        ///     The function to execute on each value from <paramref name="source"/>.
        /// </param>
        /// <returns>
        ///     The transformed source block and a disposable value that terminates the link.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="transform"/> is <see langword="null"/>.
        /// </exception>
        public static DisposableValue<ISourceBlock<TOut>> TransformManyWithNoDelta<TInput, TOut>(
            this ISourceBlock<IProjectVersionedValue<TInput>> source,
            Func<IProjectVersionedValue<TInput>, Task<IEnumerable<TOut>>> transform)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(transform, nameof(transform));

            IPropagatorBlock<IProjectVersionedValue<TInput>, TOut> transformBlock = DataflowBlockSlim.CreateTransformManyBlock(transform, skipIntermediateInputData: true, skipIntermediateOutputData: true);

            IDisposable link = source.LinkTo(transformBlock, DataflowOption.PropagateCompletion);

            return new DisposableValue<ISourceBlock<TOut>>(transformBlock, link);
        }

        /// <summary>
        ///     Creates a source block that produces a transformed value for each value from original source block,
        ///     skipping intermediate input and output states, and hence is not suitable for producing or consuming
        ///     deltas.
        /// </summary>
        /// <typeparam name="TInput">
        ///     The type of the input value produced by <paramref name="source"/>.
        /// </typeparam>
        /// <typeparam name="TOut">
        ///     The type of value produced by <paramref name="transform"/>.
        ///  </typeparam>
        /// <param name="source">
        ///     The source block whose values are to be transformed.
        /// </param>
        /// <param name="transform">
        ///     The function to execute on each value from <paramref name="source"/>.
        /// </param>
        /// <returns>
        ///     The transformed source block and a disposable value that terminates the link.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="transform"/> is <see langword="null"/>.
        /// </exception>
        public static DisposableValue<ISourceBlock<TOut>> TransformWithNoDelta<TInput, TOut>(
            this ISourceBlock<IProjectVersionedValue<TInput>> source,
            Func<IProjectVersionedValue<TInput>, TOut> transform)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(transform, nameof(transform));

            IPropagatorBlock<IProjectVersionedValue<TInput>, TOut> transformBlock = DataflowBlockSlim.CreateTransformBlock(transform, skipIntermediateInputData: true, skipIntermediateOutputData: true);

            IDisposable link = source.LinkTo(transformBlock, DataflowOption.PropagateCompletion);

            return new DisposableValue<ISourceBlock<TOut>>(transformBlock, link);
        }

        /// <summary>
        ///     Creates a source block that produces a transformed value for each value from original source block,
        ///     skipping intermediate input and output states, and hence is not suitable for producing or consuming
        ///     deltas.
        /// </summary>
        /// <typeparam name="TOut">
        ///     The type of value produced by <paramref name="transform"/>.
        ///  </typeparam>
        /// <param name="source">
        ///     The source block whose values are to be transformed.
        /// </param>
        /// <param name="transform">
        ///     The function to execute on each value from <paramref name="source"/>.
        /// </param>
        /// <param name="suppressVersionOnlyUpdates">
        ///     A value indicating whether to prevent messages from propagating to the target
        ///     block if no project changes are include other than an incremented version number.
        /// </param>
        /// <param name="ruleNames">
        ///     The names of the rules that describe the project data the caller is interested in.
        /// </param>
        /// <returns>
        ///     The transformed source block and a disposable value that terminates the link.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="transform"/> is <see langword="null"/>.
        /// </exception>
        public static DisposableValue<ISourceBlock<TOut>> TransformWithNoDelta<TOut>(
            this ISourceBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>> source,
            Func<IProjectVersionedValue<IProjectSubscriptionUpdate>, TOut> transform,
            bool suppressVersionOnlyUpdates,
            params string[] ruleNames)
        {
            return TransformWithNoDelta(source, transform, suppressVersionOnlyUpdates, (IEnumerable<string>)ruleNames);
        }

        /// <summary>
        ///     Creates a source block that produces a transformed value for each value from original source block,
        ///     skipping intermediate input and output states, and hence is not suitable for producing or consuming
        ///     deltas.
        /// </summary>
        /// <typeparam name="TOut">
        ///     The type of value produced by <paramref name="transform"/>.
        ///  </typeparam>
        /// <param name="source">
        ///     The source block whose values are to be transformed.
        /// </param>
        /// <param name="transform">
        ///     The function to execute on each value from <paramref name="source"/>.
        /// </param>
        /// <param name="suppressVersionOnlyUpdates">
        ///     A value indicating whether to prevent messages from propagating to the target
        ///     block if no project changes are include other than an incremented version number.
        /// </param>
        /// <param name="ruleNames">
        ///     The names of the rules that describe the project data the caller is interested in.
        /// </param>
        /// <returns>
        ///     The transformed source block and a disposable value that terminates the link.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="transform"/> is <see langword="null"/>.
        /// </exception>
        public static DisposableValue<ISourceBlock<TOut>> TransformWithNoDelta<TOut>(
            this ISourceBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>> source,
            Func<IProjectVersionedValue<IProjectSubscriptionUpdate>, TOut> transform,
            bool suppressVersionOnlyUpdates,
            IEnumerable<string>? ruleNames = null)
        {
            Requires.NotNull(source, nameof(source));
            Requires.NotNull(transform, nameof(transform));

            IPropagatorBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>, TOut> transformBlock = DataflowBlockSlim.CreateTransformBlock(transform, skipIntermediateInputData: true, skipIntermediateOutputData: true);

            IDisposable link = source.LinkTo(transformBlock,
                                             DataflowOption.PropagateCompletion,
                                             initialDataAsNew: true,
                                             suppressVersionOnlyUpdates: suppressVersionOnlyUpdates,
                                             ruleNames: ruleNames);

            return new DisposableValue<ISourceBlock<TOut>>(transformBlock, link);
        }

        /// <summary>
        /// Wraps a delegate in a repeatably executable delegate that runs within an ExecutionContext captured at the time of *this* method call.
        /// </summary>
        /// <typeparam name="TInput">The type of input parameter that is taken by the delegate.</typeparam>
        /// <param name="function">The delegate to invoke when the returned delegate is invoked.</param>
        /// <returns>The wrapper delegate.</returns>
        /// <remarks>
        /// This is useful because Dataflow doesn't capture or apply ExecutionContext for its delegates,
        /// so the delegate runs in whatever ExecutionContext happened to call ITargetBlock.Post, which is
        /// never the behavior we actually want. We've been bitten several times by bugs due to this.
        /// Ironically, in Dataflow's early days it *did* have the desired behavior but they removed it
        /// when they pulled it out of the Framework so it could be 'security transparent'.
        /// By passing block delegates through this wrapper, we can reattain the old behavior.
        /// </remarks>
        internal static Func<TInput, Task> CaptureAndApplyExecutionContext<TInput>(Func<TInput, Task> function)
        {
            var context = ExecutionContext.Capture();
            return input =>
            {
                SynchronizationContext currentSynchronizationContext = SynchronizationContext.Current;
                using ExecutionContext copy = context.CreateCopy();
                Task? result = null;
                ExecutionContext.Run(
                    copy,
                    state =>
                    {
                        SynchronizationContext.SetSynchronizationContext(currentSynchronizationContext);
                        result = function(input);
                    },
                    null);
                return result!;
            };
        }
    }
}
