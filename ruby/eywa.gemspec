# eywa.gemspec
Gem::Specification.new do |spec|
  spec.name          = "eywa-client"
  spec.version       = "0.3.0"
  spec.authors       = ["Robert Gersak"]
  spec.email         = ["robi@neyho.com"]

  spec.summary       = "EYWA client for asynchronous communication with EYWA server"
  spec.description   = "A Ruby gem that provides a client for EYWA server with JSON-RPC communication, GraphQL support, and task management."
  spec.homepage      = "https://github.com/neyho/eywa"
  spec.license       = "MIT"

  spec.files         = Dir["lib/**/*.rb", "README.md", "LICENSE"]
  spec.bindir        = "bin"
  spec.require_paths = ["lib"]
  
  spec.required_ruby_version = ">= 2.5.0"

  # Runtime dependencies
  spec.add_runtime_dependency "json", "~> 2.0"
  
  # Development dependencies
  spec.add_development_dependency "rake", "~> 13.0"
  spec.add_development_dependency "minitest", "~> 5.0"

  spec.metadata["homepage_uri"] = spec.homepage
  spec.metadata["source_code_uri"] = "https://github.com/neyho/eywa"
  spec.metadata["changelog_uri"] = "https://github.com/neyho/eywa/blob/main/CHANGELOG.md"
end
