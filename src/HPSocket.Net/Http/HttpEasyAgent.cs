﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HPSocket.WebSocket;

namespace HPSocket.Http
{
    public class HttpEasyAgent : HttpAgent, IHttpEasyAgent
    {
        #region 私有成员
        private readonly ExtraData<IntPtr, List<byte>> _easyData = new ExtraData<IntPtr, List<byte>>();
        private readonly ExtraData<IntPtr, WebSocketSession> _easyWsMessageData = new ExtraData<IntPtr, WebSocketSession>();
        #endregion

        /// <inheritdoc />
        public bool AutoDecompression { get; set; } = true;

        /// <inheritdoc />
        public event HttpAgentEasyDataEventHandler OnEasyChunkData;

        /// <inheritdoc />
        public event HttpAgentEasyDataEventHandler OnEasyMessageData;

        /// <inheritdoc />
        public event HttpAgentWebSocketEasyDataEventHandler OnEasyWebSocketMessageData;

#pragma warning disable 67
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyChunkData事件", true)]
        new event ChunkHeaderEventHandler OnChunkHeader;
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyChunkData事件", true)]
        new event ChunkCompleteEventHandler OnChunkComplete;
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyMessageData事件", true)]
        new event HeadersCompleteEventHandler OnHeadersComplete;
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyMessageData事件", true)]
        new event BodyEventHandler OnBody;
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyMessageData事件", true)]
        new event MessageCompleteEventHandler OnMessageComplete;
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyWebSocketMessageData事件", true)]
        new event WsMessageHeaderEventHandler OnWsMessageHeader;
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyWebSocketMessageData事件", true)]
        new event WsMessageBodyEventHandler OnWsMessageBody;
        [Obsolete("无需添加此事件, 接收到完整数据后一次性触发OnEasyWebSocketMessageData事件", true)]
        new event WsMessageCompleteEventHandler OnWsMessageComplete;
#pragma warning restore 67

        public HttpEasyAgent()
            : base(Sdk.Http.Create_HP_HttpAgentListener,
                Sdk.Http.Create_HP_HttpAgent,
                Sdk.Http.Destroy_HP_HttpAgent,
                Sdk.Http.Destroy_HP_HttpAgentListener)
        {
        }

        protected HttpEasyAgent(Sdk.CreateListenerDelegate createListenerFunction, Sdk.CreateServiceDelegate createServiceFunction, Sdk.DestroyListenerDelegate destroyServiceFunction, Sdk.DestroyListenerDelegate destroyListenerFunction)
            : base(createListenerFunction, createServiceFunction, destroyServiceFunction, destroyListenerFunction)
        {
        }


        #region 重写父类websocket相关方法, 父类相关事件不会继续触发 
        protected new HandleResult SdkOnWsMessageHeader(IntPtr sender, IntPtr connId, bool final, Rsv rsv, OpCode opCode, byte[] mask, ulong bodyLength)
        {
            if (OnEasyWebSocketMessageData == null) return HandleResult.Ignore;

            var extra = _easyWsMessageData.Get(connId) ?? new WebSocketSession();

            extra.Final = final;
            extra.Rsv = rsv;
            extra.OpCode = opCode;
            extra.Mask = mask;
            extra.Data = null;
            extra.Data = new List<byte>((int)bodyLength);

            return _easyWsMessageData.Set(connId, extra) ? HandleResult.Ok : HandleResult.Error;
        }

        protected new HandleResult SdkOnWsMessageBody(IntPtr sender, IntPtr connId, IntPtr data, int length)
        {
            if (OnEasyWebSocketMessageData == null) return HandleResult.Ignore;

            var extra = _easyWsMessageData.Get(connId);
            if (extra?.Data == null)
            {
                return HandleResult.Error;
            }

            var bytes = new byte[length];
            if (bytes.Length > 0)
            {
                Marshal.Copy(data, bytes, 0, length);
            }

            extra.Data.AddRange(bytes);

            return HandleResult.Ok;
        }

        protected new HandleResult SdkOnWsMessageComplete(IntPtr sender, IntPtr connId)
        {
            if (OnEasyWebSocketMessageData == null) return HandleResult.Ignore;

            var extra = _easyWsMessageData.Get(connId);
            if (extra == null)
            {
                return HandleResult.Error;
            }
            _easyWsMessageData.Remove(connId);

            var result = OnEasyWebSocketMessageData?.Invoke(this, connId, extra.Final, extra.Rsv, extra.OpCode, extra.Mask, extra.Data.ToArray());
            return result ?? HandleResult.Ignore;
        }

        #endregion

        #region 重写父类chunkdata相关方法, 父类相关事件不会继续触发 

        protected new HttpParseResult SdkOnChunkHeader(IntPtr sender, IntPtr connId, int length)
        {
            if (OnEasyChunkData == null) return HttpParseResult.Ok;

            var extra = new List<byte>(length);

            _easyData.Set(connId, extra);

            return _easyData.Set(connId, extra) ? HttpParseResult.Ok : HttpParseResult.Error;
        }

        protected new HttpParseResult SdkOnChunkComplete(IntPtr sender, IntPtr connId)
        {
            if (OnEasyChunkData == null) return HttpParseResult.Ok;

            var extra = _easyData.Get(connId);
            if (extra == null)
            {
                return HttpParseResult.Error;
            }
            _easyData.Remove(connId);

            var result = OnEasyChunkData?.Invoke(this, connId, extra.ToArray());
            return (HttpParseResult)result;
        }

        #endregion

        #region  重写父类message相关方法, 父类相关事件不会继续触发 

        protected new HttpParseResultEx SdkOnHeadersComplete(IntPtr sender, IntPtr connId)
        {
            if (OnEasyMessageData == null) return HttpParseResultEx.Ok;

            var header = GetHeader(connId, "Content-Length") ?? "0";
            if (!int.TryParse(header, out var contentLength))
            {
                return HttpParseResultEx.Error;
            }

            var extra = new List<byte>(contentLength);

            _easyData.Set(connId, extra);

            return _easyData.Set(connId, extra) ? HttpParseResultEx.Ok : HttpParseResultEx.Error;
        }

        protected new HttpParseResult SdkOnBody(IntPtr sender, IntPtr connId, IntPtr data, int length)
        {
            if (OnEasyMessageData == null) return HttpParseResult.Ok;

            var extra = _easyData.Get(connId);
            if (extra == null)
            {
                return HttpParseResult.Error;
            }

            var bytes = new byte[length];
            if (bytes.Length > 0)
            {
                Marshal.Copy(data, bytes, 0, length);
            }

            extra.AddRange(bytes);

            return HttpParseResult.Ok;
        }

        protected new HttpParseResult SdkOnMessageComplete(IntPtr sender, IntPtr connId)
        {
            if (OnEasyMessageData == null) return HttpParseResult.Ok;

            var extra = _easyData.Get(connId);
            if (extra == null)
            {
                return HttpParseResult.Error;
            }
            _easyData.Remove(connId);

            var data = extra.ToArray();

            if (AutoDecompression && data.Length > 0)
            {
                data = data.HttpMessageDataDecompress(GetHeader(connId, "Content-Encoding"));
            }

            var result = OnEasyMessageData?.Invoke(this, connId, data);
            return result ?? HttpParseResult.Ok;
        }

        #endregion

        /// <summary>
        /// OnClose重写用来释放资源, 该事件会继续触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="connId"></param>
        /// <param name="socketOperation"></param>
        /// <param name="errorCode"></param>
        /// <returns></returns>
        protected new HandleResult SdkOnClose(IntPtr sender, IntPtr connId, SocketOperation socketOperation, int errorCode)
        {
            _easyData.Remove(connId);
            _easyWsMessageData.Remove(connId);
            return base.SdkOnClose(sender, connId, socketOperation, errorCode);
        }

    }
}