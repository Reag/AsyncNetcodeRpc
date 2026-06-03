/*
This document is the property of Oversight Technologies Ltd that reserves its rights document and to
the data / invention / content herein described.This document, including the fact of its existence, is not to be
disclosed, in whole or in part, to any other party, and it shall not be duplicated, used, or copied in any
form, without the express prior written permission of Oversight authorized person. Acceptance of this document
will be construed as acceptance of the foregoing conditions.
*/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace AsyncNetcodeRpc.Runtime
{
    public static class AsyncRpcManagerExtensions
    {
        private static readonly Random Random = new();

        private static AsyncRpcToken CreateToken()
        {
            int tokenId = Random.Next();
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            return new AsyncRpcToken
            {
                clientId = clientId,
                tokenId = tokenId
            };
        }

        private static AsyncRpcInstance CreateAsyncRpcInstance(this AsyncRpcManager manager, AsyncRpcToken token)
        {
            return new AsyncRpcInstance(manager, token);
        }

        public static UniTask<AsyncRpcResult> PerformAsyncRpcAction(this AsyncRpcManager manager, Action<AsyncRpcToken> rpcAction,
            CancellationToken token)
        {
            var asyncToken = CreateToken();
            var actionTask = manager.AwaitCompletion(asyncToken, token);
            rpcAction.Invoke(asyncToken);
            return actionTask;
        }

        public static async UniTaskVoid PerformServerRpcAction(this AsyncRpcManager manager, AsyncRpcToken token,
            UniTask<AsyncRpcResult> serverAction)
        {
            using var completeTask = manager.CreateAsyncRpcInstance(token);
            var result = await serverAction;
            completeTask.Result = result;
        }
    }

    public class AsyncRpcInstance : IDisposable
    {
        private AsyncRpcManager Manager { get; }
        private AsyncRpcToken Token { get; }
        public AsyncRpcResult Result { get; set; }

        public AsyncRpcInstance(AsyncRpcManager manager, AsyncRpcToken token)
        {
            Manager = manager;
            Token = token;
            Result = AsyncRpcResult.Success;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Manager.SignalCompletedTask(Token, Result);
        }
    }
}