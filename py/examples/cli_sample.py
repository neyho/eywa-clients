from faker import Faker
import eywa


# Create a Faker instance and set the locale to 'en_US'
fake = Faker('en_US')

# Generate and print a random name
print("Name:", fake.name())

# Generate and print a random address
print("Address:", fake.address())

# Generate and print a random email
print("Email:", fake.email())

# Generate and print a random date
print("Birthdate:", fake.date_of_birth())

# You can also create multiple fake data entries in a loop
for _ in range(5):
    print("Random Name:", fake.name())
    print("Random Email:", fake.email())
    print("Random Address:", fake.address())
    print("Random Birthdate:", fake.date_of_birth())
    print("-" * 20)
