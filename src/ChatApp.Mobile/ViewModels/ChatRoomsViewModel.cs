using System.Collections.ObjectModel;
using ChatApp.Core.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace ChatApp.Mobile.ViewModels;

// NOTE: This file was reconstructed because it was missing from the repository.
// The optimization replaces the loop in LoadRooms with a direct assignment.
public partial class ChatRoomsViewModel : ObservableObject
{
    private ObservableCollection<ChatRoom> _rooms = new();

    public ObservableCollection<ChatRoom> Rooms
    {
        get => _rooms;
        set => SetProperty(ref _rooms, value);
    }

    public void LoadRooms(IEnumerable<ChatRoom> rooms)
    {
        if (rooms != null)
        {
            Rooms = new ObservableCollection<ChatRoom>(rooms);
        }
    }
}
