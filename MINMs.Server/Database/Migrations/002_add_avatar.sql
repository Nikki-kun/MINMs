ALTER TABLE `users` 
ADD COLUMN `avatar_id` INT NULL
AFTER `password_hash`;

ALTER TABLE `users`
ADD CONSTRAINT `fk_users_avatar`
FOREIGN KEY (`avatar_id`) REFERENCES `files`(`file_id`)
ON DELETE SET NULL ON UPDATE RESTRICT;