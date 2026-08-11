// == Guidance Session Contract == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// The contract a session model satisfies to host a Guidance Conversation: the provider its
/// Completions route to and the chat history the Guidance Turn mutates. Session models implement
/// GuidanceHistory explicitly so the alias never leaks into API serialization of the session.
/// </summary>
public interface IGuidanceSession
{
    AiProvider Provider { get; }               // Provider every turn's Completion routes to
    List<ChatMessage> GuidanceHistory { get; } // The history list a Guidance Turn appends to and trims
}
