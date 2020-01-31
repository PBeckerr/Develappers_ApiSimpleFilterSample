using System;
using System.Collections.Generic;

namespace SimpleFilterApi.Models
{
    public class Data
    {
        public class Person
        {
            public Person(Guid? id, string name)
            {
                this.Id = id;
                this.Name = name;
            }

            public int Age { get; set; }

            public Guid? Id { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return $"{nameof(this.Age)}: {this.Age}, {nameof(this.Id)}: {this.Id}, {nameof(this.Name)}: {this.Name}";
            }
        }
    }
}