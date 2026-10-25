using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Domain.Entities;

public class Customer : EntityBase
{
    public Customer() { }
    public Customer(User user, string firstName, string lastName, string address, string number)
    {
        User = user;
        UserId = user.Id;
        FirstName = firstName;
        LastName = lastName;
        Address = address;
        PhoneNumber = number;
    }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User? User { get; set; }
}
