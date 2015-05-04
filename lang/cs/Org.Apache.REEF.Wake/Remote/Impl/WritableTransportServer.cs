﻿/**
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Org.Apache.REEF.Utilities.Logging;
using Org.Apache.REEF.Wake.Util;

namespace Org.Apache.REEF.Wake.Remote.Impl
{
    /// <summary>
    /// Server to handle incoming remote messages.
    /// </summary>
    /// <typeparam name="T">Generic Type of message. It is constrained to have implemented IWritable and IType interface</typeparam>
    [Obsolete("Need to remove Iwritable and use IstreamingCodec. Please see Jira REEF-295 ", false)]
    public class WritableTransportServer<T> : IDisposable where T : IWritable
    {
        private static readonly Logger LOGGER = Logger.GetLogger(typeof (TransportServer<>));

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly IObserver<TransportEvent<T>> _remoteObserver;
        private bool _disposed;
        private Task _serverTask;

        /// <summary>
        /// Constructs a TransportServer to listen for remote events.  
        /// Listens on the specified remote endpoint.  When it recieves a remote
        /// event, it will envoke the specified remote handler.
        /// </summary>
        /// <param name="port">Port to listen on</param>
        /// <param name="remoteHandler">The handler to invoke when receiving incoming
        /// remote messages</param>
        public WritableTransportServer(int port, IObserver<TransportEvent<T>> remoteHandler)
            : this(new IPEndPoint(NetworkUtils.LocalIPAddress, port), remoteHandler)
        {
        }

        /// <summary>
        /// Constructs a TransportServer to listen for remote events.  
        /// Listens on the specified remote endpoint.  When it recieves a remote
        /// event, it will envoke the specified remote handler.
        /// </summary>
        /// <param name="localEndpoint">Endpoint to listen on</param>
        /// <param name="remoteHandler">The handler to invoke when receiving incoming
        /// remote messages</param>
        public WritableTransportServer(IPEndPoint localEndpoint,
            IObserver<TransportEvent<T>> remoteHandler)
        {
            _listener = new TcpListener(localEndpoint.Address, localEndpoint.Port);
            _remoteObserver = remoteHandler;
            _cancellationSource = new CancellationTokenSource();
            _cancellationSource.Token.ThrowIfCancellationRequested();
            _disposed = false;
        }

        /// <summary>
        /// Returns the listening endpoint for the TransportServer 
        /// </summary>
        public IPEndPoint LocalEndpoint
        {
            get { return _listener.LocalEndpoint as IPEndPoint; }
        }

        /// <summary>
        /// Starts listening for incoming remote messages.
        /// </summary>
        public void Run()
        {
            _listener.Start();
            _serverTask = Task.Run(() => StartServer());
        }

        /// <summary>
        /// Close the TransportServer and all open connections
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cancellationSource.Cancel();

                try
                {
                    _listener.Stop();
                }
                catch (SocketException)
                {
                    LOGGER.Log(Level.Info, "Disposing of transport server before listener is created.");
                }

                if (_serverTask != null)
                {
                    _serverTask.Wait();

                    // Give the TransportServer Task 500ms to shut down, ignore any timeout errors
                    try
                    {
                        CancellationTokenSource serverDisposeTimeout = new CancellationTokenSource(500);
                        _serverTask.Wait(serverDisposeTimeout.Token);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                    }
                    finally
                    {
                        _serverTask.Dispose();
                    }
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Helper method to start TransportServer.  This will
        /// be run in an asynchronous Task.
        /// </summary>
        /// <returns>An asynchronous Task for the running server.</returns>
        private async Task StartServer()
        {
            try
            {
                while (!_cancellationSource.Token.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    ProcessClient(client).Forget();
                }
            }
            catch (InvalidOperationException)
            {
                LOGGER.Log(Level.Info, "TransportServer has been closed.");
            }
            catch (OperationCanceledException)
            {
                LOGGER.Log(Level.Info, "TransportServer has been closed.");
            }
        }

        /// <summary>
        /// Recieves event from connected TcpClient and invokes handler on the event.
        /// </summary>
        /// <param name="client">The connected client</param>
        private async Task ProcessClient(TcpClient client)
        {
            // Keep reading messages from client until they disconnect or timeout
            CancellationToken token = _cancellationSource.Token;
            using (ILink<T> link = new WritableLink<T>(client))
            {
                while (!token.IsCancellationRequested)
                {
                    //T message = link.Read();
                    T message = await link.ReadAsync(token);

                    if (message == null)
                    {
                        //LOGGER.Log(Level.Error,
                   //         "ProcessClient, no message received, break." + link.RemoteEndpoint + " - " +
                      //      link.LocalEndpoint);
                        break;
                    }

                    TransportEvent<T> transportEvent = new TransportEvent<T>(message, link);

                    _remoteObserver.OnNext(transportEvent);
                }
                LOGGER.Log(Level.Error,
                    "ProcessClient close the Link. IsCancellationRequested: " + token.IsCancellationRequested);
            }
        }
    }
}