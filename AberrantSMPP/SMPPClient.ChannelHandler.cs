﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using DotNetty.Common.Utilities;

using Dawn;

using AberrantSMPP.EventObjects;
using AberrantSMPP.Packet;
using AberrantSMPP.Packet.Request;
using AberrantSMPP.Packet.Response;
using AberrantSMPP.Utility;

namespace AberrantSMPP
{
    public partial class SMPPClient
    {
        private class ChannelHandler : ChannelDuplexHandler
        {
            private readonly bool autoRelease = true;
            private readonly SMPPClient _client;
            private readonly RequestQueue _requestQueue;
            private uint _sequenceNumber;
            private States _state = States.Inactive;

            public States State => _state;

            public ChannelHandler(SMPPClient owner)
            {
                base.EnsureNotSharable();
                _client = Guard.Argument(owner, nameof(owner)).NotNull();

                var queueName = owner.SystemId + "@" + owner.Host + ":" + owner.Port.ToString();
                _requestQueue = new RequestQueue(queueName, owner.ResponseTimeout, owner.ThrowWhenAddExistingSequence, owner.RequestQueueMemoryLimitMegabytes);
            }

            private void SetNewState(IChannelHandlerContext ctx, States newState)
            {
                var oldState = _state;
                _state = newState;
                ctx.FireUserEventTriggered(new StateChangedEvent(oldState, newState));
                _client.SetNewState(newState);
            }
            
            private void ProcessOutbound(IChannelHandlerContext ctx, SmppBind bind)
            {
                GuardEx.Against(_state == States.Bound, "Trying to send Pdu over an session not yet bound?!");
                GuardEx.Against(_state != States.Connected, "Can't bind non-connected session, call Connect() first.");
                
                _Log.InfoFormat("Binding to {0}..", ctx.Channel.RemoteAddress);
                SetNewState(ctx, States.Binding);
            }

            private void ProcessOutbound(IChannelHandlerContext ctx, SmppRequest request)
            {
                if (request.SequenceNumber == 0)
                {
                    // Generate a monotonically increasing sequence number for each Pdu.
                    // When it hits the the 32 bit unsigned int maximum, it starts over.
                    request.SequenceNumber = _sequenceNumber++;
                }

                if (request.ResponseTrackingEnabled)
                {
                    _requestQueue.Add(request.SequenceNumber, request);
                }

                switch (request)
                {
                    case SmppBind bind:
                        ProcessOutbound(ctx, bind);
                        break;
                    default:
                        // do nothing, just let it go..
                        break;
                };
            }
            private void ProcessOutbound(IChannelHandlerContext ctx, Pdu packet)
            {
                Guard.Argument(packet).NotNull();
                GuardEx.Against(_state < States.Connected, "Can't send requests over a channel not yet connected.");
                GuardEx.Against(!(packet is SmppBind) && _state != States.Bound, "Can't send requests over a channel not bound to remote party.");

                if (packet is SmppRequest request)
                {
                    ProcessOutbound(ctx, request);
                }
                else if (packet is SmppResponse response)
                {
                    // Just let it flow..
                }
            }

            private void ProcessInbound(IChannelHandlerContext ctx, SmppRequest request)
            {
                GuardEx.Against( request is not SmppBind && _state != States.Bound, $"Received {request.GetType().Name} while on state {_state}?!");

                switch (request)
                {
                    case SmppBind bind:
                        if (_client.OnBind == null)
                        {
                            _Log.WarnFormat("Received SmppBind request from remote party, but no OnBind event registered?!");
                            ctx.WriteAsync( new SmppBindResp()
                            {
                                SequenceNumber = bind.SequenceNumber,
                                CommandStatus = 0, //< Answer with a default ack response
                                SystemId = "Generic"
                            });   
                        }
                        _client.OnBind?.Invoke(_client, new BindEventArgs(bind));
                        break;
                    case SmppUnbind unbind:
                        if (_client.OnUnbind == null)
                        {
                            _Log.WarnFormat("Received SmppUnbind request from remote party, but no OnUnbind event registered?!");
                            ctx.WriteAsync( new SmppUnbindResp()
                            {
                                SequenceNumber = unbind.SequenceNumber,
                                CommandStatus = 0, //< Answer with a default ack response
                            });   
                        }
                        _client.OnUnbind?.Invoke(_client, new UnbindEventArgs(unbind));
                        break;
                    case SmppGenericNack nack:
                        _client.OnGenericNack?.Invoke(_client, new GenericNackEventArgs(nack));
                        break;
                    case SmppEnquireLink enquire:
                        if (_client.OnEnquireLink == null)
                        {
                            ctx.WriteAsync( new SmppEnquireLinkResp()
                            {
                                SequenceNumber = enquire.SequenceNumber,
                                CommandStatus = 0, //< Answer with a default ack response
                            });
                        }
                        _client.OnEnquireLink?.Invoke(_client, new EnquireLinkEventArgs(enquire));
                        break;
                    case SmppAlertNotification alert:
                        _client.OnAlert?.Invoke(_client, new AlertEventArgs(alert));
                        break;
                    case SmppSubmitMulti multi:
                        if (_client.OnSubmitMulti == null)
                        {
                            ctx.WriteAsync( new SmppSubmitMultiResp()
                            {
                                SequenceNumber = multi.SequenceNumber,
                                MessageId = Guid.NewGuid().ToString().Substring(0, 10),
                                CommandStatus = 0, //< Answer with a default ack response
                            });
                        }
                        _client.OnSubmitMulti?.Invoke(_client, new SubmitMultiEventArgs(multi));
                        break;                        
                    case SmppSubmitSm submit:
                        if (_client.OnSubmitSm == null)
                        {
                            ctx.WriteAsync( new SmppSubmitSmResp()
                            {
                                SequenceNumber = submit.SequenceNumber,
                                MessageId = Guid.NewGuid().ToString().Substring(0, 10),
                                CommandStatus = 0, //< Answer with a default ack response
                            });
                        }
                        _client.OnSubmitSm?.Invoke(_client, new SubmitSmEventArgs(submit));
                        break;
                    case SmppDataSm data:
                        if (_client.OnDataSm == null)
                        {
                            ctx.WriteAsync( new SmppDataSmResp()
                            {
                                SequenceNumber = data.SequenceNumber,
                                MessageId = Guid.NewGuid().ToString().Substring(0, 10),
                                CommandStatus = 0, //< Answer with a default ack response
                            });
                        }
                        _client.OnDataSm?.Invoke(_client, new DataSmEventArgs(data));
                        break;
                    case SmppDeliverSm deliver:
                        if (_client.OnDeliverSm == null)
                        {
                            ctx.WriteAsync( new SmppDataSmResp()
                            {
                                SequenceNumber = deliver.SequenceNumber,
                                MessageId = Guid.NewGuid().ToString().Substring(0, 10),
                                CommandStatus = 0, //< Answer with a default ack response
                            });
                        }
                        _client.OnDeliverSm?.Invoke(_client, new DeliverSmEventArgs(deliver));
                        break;
                    case SmppReplaceSm replace:
                        if (_client.OnReplaceSm == null)
                        {
                            ctx.WriteAsync( new SmppReplaceSmResp()
                            {
                                SequenceNumber = replace.SequenceNumber,
                                CommandStatus = 0, //< Answer with a default ack response
                            });
                        }
                        _client.OnReplaceSm?.Invoke(_client, new ReplaceSmEventArgs(replace));
                        break;
                    case SmppQuerySm query:
                        if (_client.OnQuerySm == null)
                        {
                            ctx.WriteAsync( new SmppQuerySmResp()
                            {
                                SequenceNumber = query.SequenceNumber,
                                MessageId = Guid.NewGuid().ToString().Substring(0, 10),
                                CommandStatus = 0, //< Answer with a default ack response
                            });
                        }
                        _client.OnQuerySm?.Invoke(_client, new QuerySmEventArgs(query));
                        break;
                    case SmppCancelSm cancel:
                        _client.OnCancelSm?.Invoke(_client, new CancelSmEventArgs(cancel));
                        break;
                    default: throw new NotSupportedException($"Received not supported {request.GetType().Name} pdu from remote party?!");
                }
            }
            
            private void ProcessInbound(IChannelHandlerContext ctx, SmppResponse response)
            {
                switch (response)
                {
                    case SmppBindResp bind:
                        GuardEx.Against(_state != States.Binding, $"Received bind response while on state {_state}?!");
                        SetNewState(ctx, States.Bound);
                        _client.OnBindResp?.Invoke(_client, new BindRespEventArgs(bind));
                        break;
                    case SmppUnbindResp unbind:
                        GuardEx.Against(_state != States.Unbinding, $"Received unbind response while on state {_state}?!");
                        SetNewState(ctx, States.Connected);
                        _client.OnUnboundResp?.Invoke(_client, new UnbindRespEventArgs(unbind));
                        break;
                    case SmppGenericNackResp nack:
                        GuardEx.Against(_state != States.Connected, $"Received generic_nack response while on state {_state}?!");
                        _client?.OnGenericNackResp?.Invoke(_client, new GenericNackRespEventArgs(nack));
                        break;
                    case SmppEnquireLinkResp enquire:
                        GuardEx.Against(_state != States.Bound, $"Received enquire_link response while on state {_state}?!");
                        if (enquire.CommandStatus != CommandStatus.ESME_ROK)
                        {
                            _Log.Warn($"Received EnquireLinkResp with CommandStatus = {enquire.CommandStatus}?!");
                        }
                        _client.OnEnquireLinkResp?.Invoke(_client, new EnquireLinkRespEventArgs(enquire));
                        break;
                    case SmppDataSmResp data:
                        GuardEx.Against(_state != States.Bound, $"Received data_sm response while on state {_state}?!");
                        _client.OnDataSmResp?.Invoke(_client, new DataSmRespEventArgs(data));
                        break;
                    case SmppSubmitMultiResp multi:
                        GuardEx.Against(_state != States.Bound, $"Received submit_multi_sm response while on state {_state}?!");
                        _client.OnSubmitMultiResp?.Invoke(_client, new SubmitMultiRespEventArgs(multi));
                        break;                        
                    case SmppSubmitSmResp submit:
                        GuardEx.Against(_state != States.Bound, $"Received submit_sm response while on state {_state}?!");
                        _client.OnSubmitSmResp?.Invoke(_client, new SubmitSmRespEventArgs(submit));
                        break;
                    case SmppDeliverSmResp deliver:
                        GuardEx.Against(_state != States.Bound, $"Received deliver_sm response while on state {_state}?!");
                        _client.OnDeliverSmResp?.Invoke(_client, new DeliverSmRespEventArgs(deliver));
                        break;
                    case SmppReplaceSmResp replace:
                        GuardEx.Against(_state != States.Bound, $"Received replace_sm response while on state {_state}?!");
                        _client.OnReplaceSmResp?.Invoke(_client, new ReplaceSmRespEventArgs(replace));
                        break;
                    case SmppQuerySmResp query:
                        GuardEx.Against(_state != States.Bound, $"Received query_sm response while on state {_state}?!");
                        _client.OnQuerySmResp?.Invoke(_client, new QuerySmRespEventArgs(query));
                        break;
                    case SmppCancelSmResp cancel:
                        GuardEx.Against(_state != States.Bound, $"Received cancel response while on state {_state}?!");
                        _client.OnCancelSmResp?.Invoke(_client, new CancelSmRespEventArgs(cancel));
                        break;
                    default: throw new NotImplementedException($"FIXME: Unexpected response of type {response?.GetType().Name}?!");
                }
                
                // Handle packets related to a request awaiting response.
                if (_requestQueue.Remove(response.SequenceNumber, out var request))
                {
                    request.SetResponse(response);
                }
            }
            
            private void ProcessInbound(IChannelHandlerContext ctx, Pdu packet)
            {
                Guard.Argument(packet).NotNull();
                GuardEx.Against(_state < States.Connected, "Received a PDU over an unconnected channel?!.");
                
                if (packet is SmppGenericNack)
                {
                    packet = new SmppGenericNackResp(packet.PacketBytes); //< FIXME: Hack..
                }
                
                if (packet is SmppRequest request)
                {
                    ProcessInbound(ctx, request);
                }
                else if (packet is SmppResponse response)
                {
                    ProcessInbound(ctx, response);
                }
            }
            
            public override void HandlerAdded(IChannelHandlerContext context)
            {
                var codec = context.Channel.Pipeline.Context<PduCodec>();
                Guard.Operation(codec != null, "Missing PduCodec on HandlerPipeline.");
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                var release = true;
                try
                {
                    if (message is Pdu packet)
                    {
                        _Log.InfoFormat($"Receiving {packet.GetType().Name}..");
                        ProcessInbound(context, packet);
                    }
                    else
                    {
                        release = false;
                        base.ChannelRead(context, message);
                    }
                }
                finally
                {
                    if (autoRelease && release)
                    {
                        ReferenceCountUtil.Release(message);
                    }
                }            
            }

            public override Task WriteAsync(IChannelHandlerContext context, object message)
            {
                if (message is Pdu packet)
                {
                    _Log.InfoFormat($"Transmitting {packet.GetType().Name}..");
                    ProcessOutbound(context, packet);
                }

                //return base.WriteAsync(context, message).ContinueWith(OnWriteComplete);
                return base.WriteAsync(context, message);
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                base.ChannelActive(context);
                
                _Log.Debug("Channel activated..");
                SetNewState(context, States.Connected);
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                base.ChannelInactive(context);

               _Log.Warn("Channel de-activated..");
                SetNewState(context, States.Inactive);

                _client.OnClose?.Invoke(_client, EventArgs.Empty);
                
                // FIXME: If reconnect enabled, schedule reconnect event/action..
                //context.Channel.EventLoop.Schedule(_ => this.doConnect((EndPoint)_), context.Channel.RemoteAddress, TimeSpan.FromMilliseconds(1000));
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _client.OnError?.Invoke(_client, new SmppExceptionEventArgs(exception));
            }
        }
    }
}
