// == Chat Message Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Tests.Core;

public class ChatMessageTests
{
    [Fact]
    public void NewMessage_HasRecentTimestamp()
    {
        var before = DateTime.UtcNow;
        var message = new ChatMessage();
        var after = DateTime.UtcNow;

        Assert.InRange(message.Timestamp, before, after);
    }

    [Fact]
    public void NewMessage_DefaultsToEmptyContent()
    {
        var message = new ChatMessage();

        Assert.Equal(string.Empty, message.Content);
    }

    [Fact]
    public void Message_CanSetRoleAndContent()
    {
        var message = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Help me with this problem"
        };

        Assert.Equal(MessageRole.User, message.Role);
        Assert.Equal("Help me with this problem", message.Content);
    }

    [Fact]
    public void Message_CanSetAssistantRole()
    {
        var message = new ChatMessage { Role = MessageRole.Assistant };

        Assert.Equal(MessageRole.Assistant, message.Role);
    }
}
