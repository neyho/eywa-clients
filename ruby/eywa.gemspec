# json_rpc_handler.gemspec
Gem::Specification.new do |spec|
  spec.name          = "eywa"
  spec.version       = "0.1.0"
  spec.authors       = ["Robert Gersak"]
  spec.email         = ["r.gersak@gmail.com"]

  spec.summary       = "A EYWA client for asynchronous communication with EYWA server."
  spec.description   = "A Ruby gem that provides client for EYWA server."
  # spec.homepage      = "https://github.com/yourusername/json_rpc_handler"
  spec.license       = "MIT"

  spec.files         = Dir["lib/**/*.rb"]
  spec.bindir        = "bin"
  spec.require_paths = ["lib"]

  spec.add_dependency "json", "~> 2.0"
  spec.add_dependency "concurrent-ruby", "~> 1.1"

  # spec.metadata["source_code_uri"] = "https://github.com/yourusername/json_rpc_handler"
  # spec.metadata["changelog_uri"] = "https://github.com/yourusername/json_rpc_handler/CHANGELOG.md"
end

