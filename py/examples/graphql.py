import eywa


query = """
mutation additionalSubsidy($customer_subsidy_input: CustomerSubsidyInput) {
  syncCustomerSubsidy(customer_subsidy: $customer_subsidy_input) {
    ban
    last_name
    first_name
    ticket_id
    category
  }
}

{
  searchCustomer {
    euuid
  }
}
"""


eywa.info('hfoiqfioq')
response = eywa.graphql({'query': query, 'variables': {
    "customer-subsidy-input": {
        "ban": "527624975",
        "first-name": "f_name",
        "last-name": "l_name",
        "ticket-id": "17.02.2023 13:08/38975209909/",
        "category": "217812"
        }
    }},2)

print('Response:\n' + str(response))

eywa.close();

# {"jsonrpc":"2.0","id":0,"result":100} 
# {"jsonrpc":"2.0","id":0,"error": {"code": -32602, "messagjkkkjjjjkhhjjkdioqje": "Fucker"}} 
