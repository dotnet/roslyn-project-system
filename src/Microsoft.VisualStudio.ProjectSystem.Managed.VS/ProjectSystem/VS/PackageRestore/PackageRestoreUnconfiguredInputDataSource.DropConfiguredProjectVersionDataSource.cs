// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PackageRestore
{
    internal partial class PackageRestoreUnconfiguredInputDataSource
    {
        /// <summary>
        ///     A <see cref="IProjectValueDataSource{T}"/> that drops <see cref="ProjectDataSources.ConfiguredProjectIdentity"/> and 
        ///     <see cref="ProjectDataSources.ConfiguredProjectVersion"/> versions from each value of the original
        ///     <see cref="IProjectValueDataSource{T}"/>.
        /// </summary>
        private sealed class DropConfiguredProjectVersionDataSource<T> : IProjectValueDataSource<T>
            where T : class
        {
            private readonly IProjectValueDataSource<T> _dataSource;

            public IReceivableSourceBlock<IProjectVersionedValue<T>> SourceBlock => new DropConfiguredProjectVersionPropagator(_dataSource.SourceBlock);

            public NamedIdentity? DataSourceKey => null;

            public IComparable? DataSourceVersion => null;

            ISourceBlock<IProjectVersionedValue<object>> IProjectValueDataSource.SourceBlock => new DropConfiguredProjectVersionPropagator(_dataSource.SourceBlock);

            public DropConfiguredProjectVersionDataSource(IProjectValueDataSource<T> dataSource)
            {
                _dataSource = dataSource;
            }

            public IDisposable? Join()
            {
                return _dataSource.Join();
            }

            private class DropConfiguredProjectVersionPropagator : IPropagatorBlock<IProjectVersionedValue<T>, IProjectVersionedValue<T>>, IReceivableSourceBlock<IProjectVersionedValue<T>>
            {
                private readonly IReceivableSourceBlock<IProjectVersionedValue<T>> _source;
                private ITargetBlock<IProjectVersionedValue<T>>? _target;

                public DropConfiguredProjectVersionPropagator(IReceivableSourceBlock<IProjectVersionedValue<T>> source)
                {
                    _source = source;
                }

                public Task Completion => _source.Completion;

                public void Complete()
                {
                    _target?.Complete();
                }

#pragma warning disable CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
                public IProjectVersionedValue<T>? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<IProjectVersionedValue<T>> target, out bool messageConsumed)
#pragma warning restore CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
                {
                    IProjectVersionedValue<T>? input = _source.ConsumeMessage(messageHeader, this, out messageConsumed);
                    if (messageConsumed && input != null)
                    {
                        return DropConfiguredProjectVersion(input);
                    }

                    return null;
                }

                public void Fault(Exception exception)
                {
                    _target?.Fault(exception);
                }

                public IDisposable LinkTo(ITargetBlock<IProjectVersionedValue<T>> target, DataflowLinkOptions linkOptions)
                {
                    Requires.NotNull(target, nameof(target));
                    Interlocked.CompareExchange(ref _target, target, null);

                    if (_target != target)
                    {
                        throw new NotSupportedException();
                    }

                    return _source.LinkTo(this, linkOptions);
                }

                public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, IProjectVersionedValue<T> messageValue, ISourceBlock<IProjectVersionedValue<T>>? source, bool consumeToAccept)
                {
                    IProjectVersionedValue<T> data;
                    try
                    {
                        data = DropConfiguredProjectVersion(messageValue);
                    }
                    catch (Exception ex)
                    {
                        Fault(ex);
                        return DataflowMessageStatus.DecliningPermanently;
                    }

                    return _target!.OfferMessage(messageHeader, data, this, consumeToAccept);
                }

                public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<IProjectVersionedValue<T>> target)
                {
                    _source.ReleaseReservation(messageHeader, this);
                }

                public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<IProjectVersionedValue<T>> target)
                {
                    return _source.ReserveMessage(messageHeader, this);
                }

                private static IProjectVersionedValue<T> DropConfiguredProjectVersion(IProjectVersionedValue<T> data)
                {
                    return new ProjectVersionedValue<T>(data.Value, data.DataSourceVersions.Remove(ProjectDataSources.ConfiguredProjectIdentity)
                                                                                           .Remove(ProjectDataSources.ConfiguredProjectVersion));
                }

#pragma warning disable CS8614 // Nullability of reference types in type of parameter doesn't match implicitly implemented member.
                public bool TryReceive(Predicate<IProjectVersionedValue<T>>? filter, out IProjectVersionedValue<T>? item)
#pragma warning restore CS8614 // Nullability of reference types in type of parameter doesn't match implicitly implemented member.
                {
                    if (_source.TryReceive(
                        filter == null ? filter : (x => filter!(DropConfiguredProjectVersion(x))),
                        out item))
                    {
                        item = DropConfiguredProjectVersion(item);
                        return true;
                    }

                    return false;
                }

                public bool TryReceiveAll(out IList<IProjectVersionedValue<T>> items)
                {
                    if (_source.TryReceiveAll(out items))
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            items[i] = DropConfiguredProjectVersion(items[i]);
                        }

                        return true;
                    }

                    return false;
                }
            }
        }
    }
}
