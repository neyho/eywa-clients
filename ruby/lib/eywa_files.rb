# EYWA File Upload/Download Extensions for Ruby
# 
# This module extends the EYWA client with convenient file upload and download functionality.
# Provides high-level functions that handle the complete file lifecycle:
# - Upload files with automatic URL generation and S3 upload
# - Download files with automatic URL generation  
# - List and manage uploaded files

require 'net/http'
require 'uri'
require 'json'
require 'digest'
require 'mime/types'
require 'fileutils'

module Eywa
  class FileUploadError < StandardError; end
  class FileDownloadError < StandardError; end

  module FileOperations
    extend self

    # Upload a file to EYWA file service.
    # 
    # @param filepath [String] Path to the file to upload
    # @param options [Hash] Upload options
    # @option options [String] :name Custom name for the file (defaults to filename)
    # @option options [String] :content_type MIME type (auto-detected if not provided)
    # @option options [String] :folder_uuid UUID of parent folder (optional)
    # @option options [Proc] :progress_callback Proc called with (bytes_uploaded, total_bytes)
    # @return [Hash] File information hash
    # @raise [FileUploadError] If upload fails at any stage
    def upload_file(filepath, options = {})
      name = options[:name]
      content_type = options[:content_type]
      folder_uuid = options[:folder_uuid]
      progress_callback = options[:progress_callback]

      unless File.exist?(filepath)
        raise FileUploadError, "File not found: #{filepath}"
      end

      unless File.file?(filepath)
        raise FileUploadError, "Path is not a file: #{filepath}"
      end

      # Get file information
      file_size = File.size(filepath)
      file_name = name || File.basename(filepath)
      detected_content_type = content_type || MIME::Types.type_for(filepath).first&.content_type || 'application/octet-stream'

      info("Starting upload: #{file_name} (#{file_size} bytes)")

      begin
        # Step 1: Request upload URL
        upload_query = <<~GRAPHQL
          mutation RequestUpload($file: FileInput!) {
              requestUploadURL(file: $file)
          }
        GRAPHQL

        variables = {
          file: {
            name: file_name,
            content_type: detected_content_type,
            size: file_size
          }
        }

        variables[:file][:folder] = { euuid: folder_uuid } if folder_uuid

        result_thread = graphql(upload_query, variables)
        result = result_thread.value
        upload_url = result['data']['requestUploadURL']

        debug("Upload URL received: #{upload_url[0..50]}...")

        # Step 2: Upload file to S3
        file_data = File.read(filepath)

        progress_callback&.call(0, file_size)

        response = http_put_request(upload_url, file_data, {
          'Content-Type' => detected_content_type
        })

        unless response.code.to_i == 200
          raise FileUploadError, "S3 upload failed (#{response.code}): #{response.body}"
        end

        progress_callback&.call(file_size, file_size)

        debug('File uploaded to S3 successfully')

        # Step 3: Confirm upload
        confirm_query = <<~GRAPHQL
          mutation ConfirmUpload($url: String!) {
              confirmFileUpload(url: $url)
          }
        GRAPHQL

        confirm_thread = graphql(confirm_query, { url: upload_url })
        confirm_result = confirm_thread.value

        unless confirm_result['data']['confirmFileUpload']
          raise FileUploadError, 'Upload confirmation failed'
        end

        debug('Upload confirmed')

        # Step 4: Get file information
        file_info = get_file_by_name(file_name)
        unless file_info
          raise FileUploadError, 'Could not retrieve uploaded file information'
        end

        info("Upload completed: #{file_name} -> #{file_info['euuid']}")
        file_info

      rescue => e
        error("Upload failed: #{e.message}")
        raise FileUploadError, "Upload failed: #{e.message}"
      end
    end

    # Upload content directly from memory.
    # 
    # @param content [String] Content to upload
    # @param name [String] Filename for the content
    # @param options [Hash] Upload options
    # @option options [String] :content_type MIME type (default: 'text/plain')
    # @option options [String] :folder_uuid UUID of parent folder (optional)
    # @option options [Proc] :progress_callback Proc called with (bytes_uploaded, total_bytes)
    # @return [Hash] File information hash
    # @raise [FileUploadError] If upload fails
    def upload_content(content, name, options = {})
      content_type = options[:content_type] || 'text/plain'
      folder_uuid = options[:folder_uuid]
      progress_callback = options[:progress_callback]

      content = content.encode('UTF-8') if content.is_a?(String)
      file_size = content.bytesize

      info("Starting content upload: #{name} (#{file_size} bytes)")

      begin
        # Step 1: Request upload URL
        upload_query = <<~GRAPHQL
          mutation RequestUpload($file: FileInput!) {
              requestUploadURL(file: $file)
          }
        GRAPHQL

        variables = {
          file: {
            name: name,
            content_type: content_type,
            size: file_size
          }
        }

        variables[:file][:folder] = { euuid: folder_uuid } if folder_uuid

        result_thread = graphql(upload_query, variables)
        result = result_thread.value
        upload_url = result['data']['requestUploadURL']

        # Step 2: Upload content to S3
        progress_callback&.call(0, file_size)

        response = http_put_request(upload_url, content, {
          'Content-Type' => content_type
        })

        unless response.code.to_i == 200
          raise FileUploadError, "S3 upload failed (#{response.code}): #{response.body}"
        end

        progress_callback&.call(file_size, file_size)

        # Step 3: Confirm upload
        confirm_query = <<~GRAPHQL
          mutation ConfirmUpload($url: String!) {
              confirmFileUpload(url: $url)
          }
        GRAPHQL

        confirm_thread = graphql(confirm_query, { url: upload_url })
        confirm_result = confirm_thread.value

        unless confirm_result['data']['confirmFileUpload']
          raise FileUploadError, 'Upload confirmation failed'
        end

        # Step 4: Get file information
        file_info = get_file_by_name(name)
        unless file_info
          raise FileUploadError, 'Could not retrieve uploaded file information'
        end

        info("Content upload completed: #{name} -> #{file_info['euuid']}")
        file_info

      rescue => e
        error("Content upload failed: #{e.message}")
        raise FileUploadError, "Content upload failed: #{e.message}"
      end
    end

    # Download a file from EYWA file service.
    # 
    # @param file_uuid [String] UUID of the file to download
    # @param save_path [String, nil] Path to save the file (if nil, returns content)
    # @param progress_callback [Proc, nil] Proc called with (bytes_downloaded, total_bytes)
    # @return [String] Path to saved file or file content
    # @raise [FileDownloadError] If download fails
    def download_file(file_uuid, save_path = nil, progress_callback = nil)
      info("Starting download: #{file_uuid}")

      begin
        # Step 1: Request download URL
        download_query = <<~GRAPHQL
          query RequestDownload($file: FileInput!) {
              requestDownloadURL(file: $file)
          }
        GRAPHQL

        result_thread = graphql(download_query, { file: { euuid: file_uuid } })
        result = result_thread.value
        download_url = result['data']['requestDownloadURL']

        debug("Download URL received: #{download_url[0..50]}...")

        # Step 2: Download file
        response = http_get_request(download_url, progress_callback)

        unless response.code.to_i == 200
          raise FileDownloadError, "Download failed (#{response.code}): #{response.body}"
        end

        content = response.body

        if save_path
          # Save to file
          FileUtils.mkdir_p(File.dirname(save_path))
          File.write(save_path, content, mode: 'wb')

          info("Download completed: #{file_uuid} -> #{save_path}")
          save_path
        else
          info("Download completed: #{file_uuid} (#{content.bytesize} bytes)")
          content
        end

      rescue => e
        error("Download failed: #{e.message}")
        raise FileDownloadError, "Download failed: #{e.message}"
      end
    end

    # List files in EYWA file service.
    # 
    # @param options [Hash] Filter options
    # @option options [Integer] :limit Maximum number of files to return
    # @option options [String] :status Filter by status (PENDING, UPLOADED, etc.)
    # @option options [String] :name_pattern Filter by name pattern (SQL LIKE)
    # @option options [String] :folder_uuid Filter by folder UUID
    # @return [Array<Hash>] List of file hashes
    def list_files(options = {})
      limit = options[:limit]
      status = options[:status]
      name_pattern = options[:name_pattern]
      folder_uuid = options[:folder_uuid]

      debug("Listing files (limit=#{limit}, status=#{status})")

      begin
        query = <<~GRAPHQL
          query ListFiles($limit: Int, $where: FileWhereInput) {
              searchFile(_limit: $limit, _where: $where, _order_by: {uploaded_at: desc}) {
                  euuid
                  name
                  status
                  content_type
                  size
                  uploaded_at
                  uploaded_by {
                      name
                  }
                  folder {
                      euuid
                      name
                  }
              }
          }
        GRAPHQL

        where_conditions = []

        where_conditions << { status: { _eq: status } } if status
        where_conditions << { name: { _ilike: "%#{name_pattern}%" } } if name_pattern
        where_conditions << { folder: { euuid: { _eq: folder_uuid } } } if folder_uuid

        variables = {}
        variables[:limit] = limit if limit

        if where_conditions.any?
          variables[:where] = if where_conditions.length == 1
                                where_conditions.first
                              else
                                { _and: where_conditions }
                              end
        end

        result_thread = graphql(query, variables)
        result = result_thread.value
        files = result['data']['searchFile']

        debug("Found #{files.length} files")
        files

      rescue => e
        error("Failed to list files: #{e.message}")
        raise
      end
    end

    # Get information about a specific file.
    # 
    # @param file_uuid [String] UUID of the file
    # @return [Hash, nil] File information hash or nil if not found
    def get_file_info(file_uuid)
      query = <<~GRAPHQL
        query GetFile($uuid: UUID!) {
            getFile(euuid: $uuid) {
                euuid
                name
                status
                content_type
                size
                uploaded_at
                uploaded_by {
                    name
                }
                folder {
                    euuid
                    name
                }
            }
        }
      GRAPHQL

      begin
        result_thread = graphql(query, { uuid: file_uuid })
        result = result_thread.value
        result['data']['getFile']
      rescue => e
        debug("File not found or error: #{e.message}")
        nil
      end
    end

    # Get file information by name (returns most recent if multiple).
    # 
    # @param name [String] File name to search for
    # @return [Hash, nil] File information hash or nil if not found
    def get_file_by_name(name)
      files = list_files(limit: 1, name_pattern: name)
      files.first
    end

    # Delete a file from EYWA file service.
    # 
    # @param file_uuid [String] UUID of the file to delete
    # @return [Boolean] True if deletion successful
    def delete_file(file_uuid)
      query = <<~GRAPHQL
        mutation DeleteFile($uuid: UUID!) {
            deleteFile(euuid: $uuid)
        }
      GRAPHQL

      begin
        result_thread = graphql(query, { uuid: file_uuid })
        result = result_thread.value
        success = result['data']['deleteFile']

        if success
          info("File deleted: #{file_uuid}")
        else
          warn("File deletion failed: #{file_uuid}")
        end

        success
      rescue => e
        error("Failed to delete file: #{e.message}")
        false
      end
    end

    # Calculate hash of a file for integrity verification.
    # 
    # @param filepath [String] Path to the file
    # @param algorithm [String] Hash algorithm ('md5', 'sha1', 'sha256', etc.)
    # @return [String] Hex digest of the file hash
    def calculate_file_hash(filepath, algorithm = 'sha256')
      digest_class = case algorithm.downcase
                     when 'md5' then Digest::MD5
                     when 'sha1' then Digest::SHA1
                     when 'sha256' then Digest::SHA256
                     when 'sha512' then Digest::SHA512
                     else raise ArgumentError, "Unsupported algorithm: #{algorithm}"
                     end

      digest_class.file(filepath).hexdigest
    end

    # Quick upload with minimal parameters.
    # 
    # @param filepath [String] Path to file to upload
    # @return [String] File UUID
    def quick_upload(filepath)
      result = upload_file(filepath)
      result['euuid']
    end

    # Quick download to current directory.
    # 
    # @param file_uuid [String] UUID of file to download
    # @param filename [String, nil] Custom filename (auto-detected if not provided)
    # @return [String] Path to downloaded file
    def quick_download(file_uuid, filename = nil)
      if filename.nil?
        file_info = get_file_info(file_uuid)
        filename = file_info ? file_info['name'] : "download_#{file_uuid[0..7]}"
      end

      download_file(file_uuid, filename)
    end

    private

    # Helper method for HTTP PUT requests
    def http_put_request(url, data, headers = {})
      uri = URI(url)
      http = Net::HTTP.new(uri.host, uri.port)
      http.use_ssl = (uri.scheme == 'https')

      request = Net::HTTP::Put.new(uri)
      headers.each { |key, value| request[key] = value }
      request.body = data

      http.request(request)
    end

    # Helper method for HTTP GET requests with progress callback
    def http_get_request(url, progress_callback = nil)
      uri = URI(url)
      http = Net::HTTP.new(uri.host, uri.port)
      http.use_ssl = (uri.scheme == 'https')

      request = Net::HTTP::Get.new(uri)
      
      response = http.request(request) do |res|
        if progress_callback
          total_size = res['content-length']&.to_i || 0
          downloaded = 0
          
          res.read_body do |chunk|
            downloaded += chunk.length
            progress_callback.call(downloaded, total_size) if total_size > 0
          end
        else
          res.read_body
        end
      end

      response
    end
  end

  # Include file operations in the main client
  class Client
    include FileOperations
  end
end

# Include file operations in the global module for backward compatibility
module Eywa::GlobalClient
  include Eywa::FileOperations
end
